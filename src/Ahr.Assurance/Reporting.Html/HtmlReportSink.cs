using System.Collections.Immutable;
using System.Net;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting.Html;

internal sealed class HtmlReportSink(HtmlReportSinkOptions? options = null) : IAssuranceEvidenceSink
{
    public string Name => "HTML";

    public async Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);
        StakeholderReportWriter writer = new(options?.MaxTextLength ?? 4096);
        var directory = Path.GetFullPath(options?.OutputDirectory ?? context.DefaultOutputDirectory, AppContext.BaseDirectory);
        var path = Path.Combine(directory, EvidenceFiles.SafeId(evidence.VerificationId) + ".html");
        (ImmutableArray<EvidenceAttachment> attachments, var attachmentsDirectory, ImmutableArray<ReportDiagnostic> diagnostics) =
            await AttachmentBundle.PlaceAsync(evidence, context, directory, cancellationToken).ConfigureAwait(false);
        VerificationEvidence exported = EvidenceExport.Project(evidence with
        {
            Attachments = attachments,
            Report = evidence.Report with
            {
                Diagnostics = evidence.Report.Diagnostics.AddRange(diagnostics)
            }
        }, context.IncludeDiagnosticDetails);
        ScenarioResult[] scenarios = [.. exported.Report.Run.Scenarios];
        ImmutableArray<ReportAttachmentView> views = [.. exported.Attachments.Select(attachment => View(attachment, scenarios, directory, attachmentsDirectory))];
        DeveloperReportSection developer = new($"<p>Verification: {WebUtility.HtmlEncode(exported.VerificationId)}. Execution: {exported.ExecutionState}."
            + " Suite completion not established.</p>" + writer.Diagnostics(exported.Report.Diagnostics));
        var title = string.IsNullOrWhiteSpace(exported.Report.Run.Name) ? StakeholderReportWriter.DefaultTitle : exported.Report.Run.Name;
        var html = writer.Render(exported.Report, title, views, developer);
        await ReportArtifactWriter.WriteAsync(async (stream, token) =>
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(html), token).ConfigureAwait(false), path, cancellationToken).ConfigureAwait(false);
        return new([new(Name, path)], diagnostics);
    }

    private static ReportAttachmentView View(EvidenceAttachment attachment, ScenarioResult[] scenarios, string directory, string attachmentsDirectory)
    {
        int? scenario = attachment.ScenarioId is { } id && Array.FindIndex(scenarios, value => value.Id == id) is var index and >= 0 ? index : null;
        if (!attachment.Available)
        {
            return new(scenario, attachment.StepIndex, attachment.Name, Problem: "was not available when this run was recorded.");
        }

        var file = Path.GetFullPath(attachment.Reference, attachmentsDirectory);
        return File.Exists(file)
            ? new(scenario, attachment.StepIndex, attachment.Name, Link: Link(directory, file), Image: StakeholderReportWriter.IsImage(attachment.MediaType))
            : new(scenario, attachment.StepIndex, attachment.Name, Problem: "is not available.");
    }

    private static string Link(string directory, string file) =>
        string.Join('/', Path.GetRelativePath(directory, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Select(Uri.EscapeDataString));
}
