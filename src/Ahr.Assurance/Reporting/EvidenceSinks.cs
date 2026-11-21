using System.Collections.Immutable;

namespace Ahr.Assurance.Reporting;

/// <summary>An output that receives each verification's evidence, such as the built-in ones in <see cref="ReportSinks"/>.</summary>
public interface IAssuranceEvidenceSink
{
    /// <summary>The sink's name, shown on the files it writes and in its diagnostics.</summary>
    string Name { get; }
    /// <summary>Shared instances must support concurrent calls. The coordinator does not dispose them.</summary>
    Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default);
}

/// <summary>What a sink delivered for one verification.</summary>
/// <param name="Artifacts">The files the sink wrote.</param>
/// <param name="Diagnostics">The problems the sink met.</param>
public sealed record EvidenceDeliveryResult(
    ImmutableArray<AssuranceArtifact> Artifacts, ImmutableArray<ReportDiagnostic> Diagnostics)
{
    /// <summary>A result with no files and no problems.</summary>
    public static EvidenceDeliveryResult Empty { get; } = new([], []);
}

/// <summary>Where and how a sink delivers a verification's evidence.</summary>
/// <param name="DefaultOutputDirectory">The full path of the folder sinks write to when they have no folder of their own.</param>
public sealed record EvidenceDeliveryContext(string DefaultOutputDirectory)
{
    internal IAttachmentContentSource? AttachmentSource { get; init; }

    /// <summary>The full path of the folder that attachment references are relative to, when there is one.</summary>
    public string? AttachmentsDirectory { get; init; }
    /// <summary>Whether to keep raw operational exception details. See <see cref="ReportOptions.IncludeDiagnosticDetails"/>.</summary>
    public bool IncludeDiagnosticDetails { get; init; }
    /// <summary><see langword="true"/> when the verification, its attachment copy or an earlier delivery attempt was canceled. The
    /// sink then runs under a short time limit instead of the caller's token, and the evidence may be partial.</summary>
    public bool IsRecovery { get; init; }
}

/// <summary>Options for the JSON evidence sink from <see cref="ReportSinks.Json"/>.</summary>
public sealed record JsonReportSinkOptions
{
    /// <summary>The folder to write evidence files to. A relative path is resolved against <see cref="AppContext.BaseDirectory"/>.
    /// Defaults to <see cref="EvidenceDeliveryContext.DefaultOutputDirectory"/>, the current run's folder.</summary>
    public string? OutputDirectory { get; init; }
    /// <summary>Whether to indent the JSON. Defaults to <see langword="true"/>.</summary>
    public bool WriteIndented { get; init; } = true;
}

internal sealed class ArtifactReportSink(AssuranceReportOutput output) : IAssuranceEvidenceSink
{
    private readonly AssuranceReportOutput _output = output ?? throw new ArgumentNullException(nameof(output));
    public string Name => _output.Name;

    public async Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var location = _output.Destination.ResolveLocation(evidence.VerificationId);
        await _output.Destination.WriteAsync(_output.Reporter, evidence.Report, location, cancellationToken).ConfigureAwait(false);
        return new([new(Name, location)], []);
    }
}

internal sealed class JsonReportSink(JsonReportSinkOptions? options = null) : IAssuranceEvidenceSink
{
    public string Name => "JSON";

    public async Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);
        var directory = Path.GetFullPath(options?.OutputDirectory ?? context.DefaultOutputDirectory, AppContext.BaseDirectory);
        var path = Path.Combine(directory, EvidenceFiles.SafeId(evidence.VerificationId) + ".json");
        (ImmutableArray<EvidenceAttachment> attachments, var attachmentsDirectory, ImmutableArray<ReportDiagnostic> diagnostics) =
            await AttachmentBundle.PlaceAsync(evidence, context, directory, cancellationToken).ConfigureAwait(false);
        VerificationEvidence persisted = EvidenceExport.Project(evidence with
        {
            Attachments = attachments,
            AttachmentsDirectory = attachments.IsEmpty ? null : Path.GetRelativePath(directory, attachmentsDirectory).Replace('\\', '/'),
            Report = evidence.Report with
            {
                Diagnostics = evidence.Report.Diagnostics.AddRange(diagnostics)
            }
        }, context.IncludeDiagnosticDetails);
        await ReportArtifactWriter.WriteAsync(async (stream, token) =>
        {
            var bytes = EvidenceDocumentJson.SerializeToUtf8Bytes(persisted, options?.WriteIndented ?? true);
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        }, path, cancellationToken).ConfigureAwait(false);
        return new([new(Name, path)], diagnostics);
    }
}

internal static class EvidenceFiles
{
    internal static string SafeId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id.Length > 160 || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? throw new InvalidDataException("Artifact identities must use at most 160 ASCII letters, digits, hyphens, or underscores.")
            : id;
    }
}
