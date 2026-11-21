using Ahr.Foundation;
namespace Ahr.Assurance.Reporting;

internal static class EvidencePublisher
{
    public static async Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, IEnumerable<IAssuranceEvidenceSink> sinks, EvidenceDeliveryContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentNullException.ThrowIfNull(context);
        IAssuranceEvidenceSink[] selected = [.. sinks];
        ValidateSinks(selected);
        if (selected.Length == 0)
        {
            return new([], []);
        }

        List<EvidenceDeliveryResult> deliveries = new(selected.Length);
        foreach (IAssuranceEvidenceSink sink in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<EvidenceDeliveryResult, ReportDiagnostic> delivery = await DeliverAsync(sink, evidence, context, cancellationToken).ConfigureAwait(false);
            deliveries.Add(delivery.Match(static result => result, static failure => new EvidenceDeliveryResult([], [failure])));
        }
        return new([.. deliveries.SelectMany(static delivery => delivery.Artifacts)], [.. deliveries.SelectMany(static delivery => delivery.Diagnostics)]);
    }

    private static async Task<Result<EvidenceDeliveryResult, ReportDiagnostic>> DeliverAsync(
        IAssuranceEvidenceSink sink, VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken)
    {
        try
        {
            Result<Option<EvidenceDeliveryResult>, ReportDiagnostic> published = await Result.TryAsync(
                async () => (await sink.PublishAsync(evidence, context, cancellationToken).ConfigureAwait(false)).ToOption(),
                static _ => true,
                exception => SinkFailure(sink, exception)).ConfigureAwait(false);
            return published
                .Bind(result => result.ToResult(new ReportDiagnostic("report-sink-error",
                    $"Evidence sink '{sink.Name}' failed: Evidence sink returned no delivery result.", typeof(InvalidOperationException).FullName)))
                .Ensure(IsValid, new ReportDiagnostic("report-sink-error", $"Evidence sink '{sink.Name}' returned an invalid delivery result."));
        }
        catch (OperationCanceledException exception) when (context.IsRecovery && !cancellationToken.IsCancellationRequested)
        {
            return Result<EvidenceDeliveryResult, ReportDiagnostic>.Failure(SinkFailure(sink, exception));
        }
    }

    private static ReportDiagnostic SinkFailure(IAssuranceEvidenceSink sink, Exception exception) =>
        new("report-sink-error", $"Evidence sink '{sink.Name}' {Outcome(exception)}: {exception.Message}", exception.GetType().FullName);

    private static string Outcome(Exception exception) => exception is OperationCanceledException ? "canceled recovery" : "failed";

    private static bool IsValid(EvidenceDeliveryResult delivery) =>
        !delivery.Artifacts.IsDefault && !delivery.Diagnostics.IsDefault &&
        delivery.Artifacts.All(item => item is not null && !string.IsNullOrWhiteSpace(item.Location)) &&
        delivery.Diagnostics.All(item => item is not null);

    internal static void ValidateSinks(IEnumerable<IAssuranceEvidenceSink> sinks)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (IAssuranceEvidenceSink sink in sinks)
        {
            ArgumentNullException.ThrowIfNull(sink);
            ArgumentException.ThrowIfNullOrWhiteSpace(sink.Name);
            if (!names.Add(sink.Name))
            {
                throw new ArgumentException($"Duplicate evidence sink name '{sink.Name}'.", nameof(sinks));
            }
        }
    }
}
