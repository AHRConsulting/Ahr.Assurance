using System.Collections.Immutable;
using Ahr.Assurance.Execution;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class AttachmentBundle
{
    internal static string Extension(EvidenceAttachment attachment) => attachment.MediaType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "video/mp4" => ".mp4",
        _ => Path.GetExtension(attachment.Reference) is { Length: > 1 and <= 11 } extension && extension.Skip(1).All(char.IsAsciiLetterOrDigit)
            ? extension
            : ".bin"
    };

    public static async Task<(ImmutableArray<EvidenceAttachment> Attachments, ImmutableArray<ReportDiagnostic> Diagnostics)> MaterializeAsync(
        ImmutableArray<EvidenceAttachment> attachments, IAttachmentContentSource? source, string directory, string verificationId,
        CancellationToken cancellationToken = default)
    {
        if (attachments.IsEmpty)
        {
            return ([], []);
        }
        List<EvidenceAttachment> materialized = [];
        List<ReportDiagnostic> diagnostics = [];
        foreach (EvidenceAttachment attachment in attachments)
        {
            if (!attachment.Available)
            {
                if (attachment.Reference != AttachmentCapture.MissingReference)
                {
                    diagnostics.Add(new("report-attachment-unavailable", "Attachment content was not available in the exported evidence.", DetailsWithheld: attachment.DetailsWithheld));
                }
                materialized.Add(attachment);
                continue;
            }
            Result<EvidenceAttachment, ReportDiagnostic> copied = await CopyAsync(attachment, source, directory, verificationId, cancellationToken)
                .ConfigureAwait(false);
            materialized.Add(copied.Match(static copy => copy, _ => attachment with
            {
                Available = false
            }));
            _ = copied.TapError(diagnostics.Add);
        }
        return ([.. materialized], [.. diagnostics]);
    }

    private static async Task<Result<EvidenceAttachment, ReportDiagnostic>> CopyAsync(EvidenceAttachment attachment, IAttachmentContentSource? source,
        string directory, string verificationId, CancellationToken cancellationToken)
    {
        var reference = $"{EvidenceFiles.SafeId(verificationId)}-{EvidenceFiles.SafeId(attachment.Id)}{Extension(attachment)}";
        return await source.ToOption()
            .ToResult(DeliveryError(attachment, "No attachment content source was supplied.", typeof(InvalidOperationException)))
            .BindAsync(content => Result.TryAsync(
                async () =>
                {
                    await ReportArtifactWriter.WriteAsync(async (stream, token) =>
                    {
                        await using Stream opened = await content.OpenReadAsync(attachment, token).ConfigureAwait(false);
                        await opened.CopyToAsync(stream, token).ConfigureAwait(false);
                    }, Path.Combine(directory, reference), cancellationToken).ConfigureAwait(false);
                    return attachment with
                    {
                        Reference = reference
                    };
                },
                exception => exception is not OperationCanceledException,
                exception => DeliveryError(attachment, exception.Message, exception.GetType()))).ConfigureAwait(false);
    }

    private static ReportDiagnostic DeliveryError(EvidenceAttachment attachment, string message, Type type) =>
        new("report-attachment-delivery-error", $"Attachment '{attachment.Name}': {message}", type.FullName);

    internal static async Task<(ImmutableArray<EvidenceAttachment> Attachments, string Directory, ImmutableArray<ReportDiagnostic> Diagnostics)> PlaceAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, string directory, CancellationToken cancellationToken = default)
    {
        if (context.AttachmentsDirectory is { } recorded)
        {
            return (evidence.Attachments, recorded, []);
        }

        var folder = Path.Combine(directory, "attachments");
        (ImmutableArray<EvidenceAttachment> attachments, ImmutableArray<ReportDiagnostic> diagnostics) =
            await MaterializeAsync(evidence.Attachments, context.AttachmentSource, folder, evidence.VerificationId, cancellationToken).ConfigureAwait(false);
        return (attachments, folder, diagnostics);
    }
}

internal sealed class AttachmentFolderSource(string root) : IAttachmentContentSource
{
    private readonly string _root = Path.GetFullPath(root);

    public Task<Stream> OpenReadAsync(EvidenceAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(Path.GetFullPath(attachment.Reference, _root), FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 4096, useAsync: true));
    }
}
