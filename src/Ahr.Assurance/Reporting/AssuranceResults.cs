using System.Collections.Immutable;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

/// <summary>The verification evidence in one results folder, such as a run folder: its top-level JSON files, in file name order.</summary>
/// <remarks>A folder can hold verifications from more than one run. Each one's <see cref="VerificationEvidence.CorrelationId"/> says which.</remarks>
public sealed class AssuranceResults
{
    private readonly string? _attachmentsDirectory;

    internal AssuranceResults(string directory, ImmutableArray<VerificationEvidence> verifications, string? attachmentsDirectory = null)
    {
        Directory = directory;
        Verifications = verifications;
        _attachmentsDirectory = attachmentsDirectory;
    }

    /// <summary>The full path of the folder the evidence was loaded from.</summary>
    public string Directory { get; }

    /// <summary>The verifications loaded, in file name order.</summary>
    public ImmutableArray<VerificationEvidence> Verifications { get; }

    /// <summary>The folder a verification's attachment references are relative to: the folder given when loading,
    /// or the one recorded in its evidence, relative to <see cref="Directory"/>.</summary>
    public string GetAttachmentsDirectory(VerificationEvidence verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return _attachmentsDirectory ?? Path.GetFullPath(verification.AttachmentsDirectory ?? ".", Directory);
    }

    /// <summary>The full path of an attachment's file, or <see langword="null"/> when it was not available when the run was recorded.
    /// The file may since have been moved or removed.</summary>
    public string? GetAttachmentPath(VerificationEvidence verification, EvidenceAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return attachment.Available ? Path.GetFullPath(attachment.Reference, GetAttachmentsDirectory(verification)) : null;
    }

    /// <summary>Loads the top-level JSON evidence files in <paramref name="path"/>, or one evidence file.</summary>
    /// <param name="path">A results folder, or one evidence file.</param>
    /// <param name="attachmentsDirectory">The folder holding the attachments, when it was moved or uploaded apart from the evidence.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <exception cref="DirectoryNotFoundException">Neither a folder nor a file exists at <paramref name="path"/>.</exception>
    /// <exception cref="InvalidDataException">A file is not valid evidence. The message names the file.</exception>
    public static async Task<AssuranceResults> LoadAsync(string path, string? attachmentsDirectory = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        string[] files = System.IO.Directory.Exists(full)
            ? [.. System.IO.Directory.GetFiles(full, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)]
            : File.Exists(full) ? [full] : throw new DirectoryNotFoundException($"No results folder or evidence file at {full}.");
        Result<IReadOnlyList<VerificationEvidence>> verifications = await files
            .TraverseAsync(file => ReadAsync(file, cancellationToken)).ConfigureAwait(false);
        return verifications
            .Map(loaded => new AssuranceResults(System.IO.Directory.Exists(full) ? full : Path.GetDirectoryName(full)!, [.. loaded],
                attachmentsDirectory is null ? null : Path.GetFullPath(attachmentsDirectory)))
            .OrThrow(static error => new InvalidDataException(error.Message, error.Exception));
    }

    private static async Task<Result<VerificationEvidence>> ReadAsync(string file, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        return EvidenceDocumentJson.Read(json).MapError(error => error with { Message = $"{file} is not valid evidence: {error.Message}" });
    }
}
