using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

internal sealed class AttachmentScope : IDisposable
{
    private const int NoStep = -1;
    private static readonly AsyncLocal<AttachmentScope?> _current = new();
    private readonly AttachmentCapture? _capture;
    private readonly AttachmentScope? _previous;
    private volatile bool _open = true;
    private volatile string? _scenarioId;
    private volatile int _stepIndex = NoStep;
    private ExecutionContext? _stepContext;

    private AttachmentScope(AttachmentCapture? capture)
    {
        _capture = capture;
        _previous = _current.Value;
        _current.Value = this;
    }

    internal static AttachmentScope Current => _current.Value is { _open: true } scope
        ? scope
        : throw new InvalidOperationException("Attach can only be called while a scenario is running.");

    internal static AttachmentScope Enter(AttachmentCapture? capture) => new(capture);

    internal void EnterScenario(string scenarioId)
    {
        _stepIndex = NoStep;
        _stepContext = null;
        _scenarioId = scenarioId;
    }

    internal void EnterStep(int stepIndex) => _stepIndex = stepIndex;

    internal void ExitStep() => _stepIndex = NoStep;

    internal static void KeepStepContext()
    {
        if (_current.Value is { } scope)
        {
            scope._stepContext ??= ExecutionContext.Capture();
        }
    }

    internal void RestoreStepContext()
    {
        if (_stepContext is { } context)
        {
            _stepContext = null;
            ExecutionContext.Restore(context);
        }
    }

    internal void AddFile(string name, string mediaType, string path, AttachmentKeep keep) =>
        _capture?.Add(new(_scenarioId, StepIndex(), name, mediaType, path, null), keep);

    internal void AddContent(string name, string mediaType, Action<string> write, AttachmentKeep keep) =>
        _capture?.Add(new(_scenarioId, StepIndex(), name, mediaType, null, write), keep);

    internal void FinishScenario(ScenarioResult result) => _capture?.Settle(_scenarioId, result.Status == AssuranceStatus.Satisfied);

    private int? StepIndex() => _stepIndex is var stepIndex && stepIndex == NoStep ? null : stepIndex;

    public void Dispose()
    {
        _open = false;
        _current.Value = _previous;
    }
}

internal sealed class AttachmentCapture(string directory, string verificationId)
{
    internal const string MissingReference = "missing";
    private readonly Lock _gate = new();
    private readonly List<(EvidenceAttachment Attachment, Task? Write)> _attachments = [];
    private readonly List<HeldAttachment> _held = [];

    internal string Directory => directory;

    internal void Add(HeldAttachment attachment, AttachmentKeep keep)
    {
        lock (_gate)
        {
            if (keep == AttachmentKeep.OnFailure)
            {
                _held.Add(attachment);
                return;
            }

            Keep(attachment);
        }
    }

    internal void Settle(string? scenarioId, bool passed)
    {
        lock (_gate)
        {
            HeldAttachment[] settled = [.. _held.Where(held => held.ScenarioId == scenarioId)];
            _ = _held.RemoveAll(held => held.ScenarioId == scenarioId);
            if (!passed)
            {
                Array.ForEach(settled, Keep);
            }
        }
    }

    private void Keep(HeldAttachment attachment)
    {
        var id = "attached-" + (_attachments.Count + 1);
        if (attachment.Write is null)
        {
            var linked = Path.GetRelativePath(directory, Path.GetFullPath(attachment.FilePath!)).Replace('\\', '/');
            _attachments.Add((Evidence(attachment, id, linked), null));
            return;
        }

        var reference = EvidenceFiles.SafeId(verificationId) + "-" + id + Extension(attachment.Name, attachment.MediaType);
        var path = Path.Combine(directory, reference);
        _attachments.Add((Evidence(attachment, id, reference), Task.Run(() =>
        {
            _ = System.IO.Directory.CreateDirectory(directory);
            attachment.Write(path);
        })));
    }

    private static EvidenceAttachment Evidence(HeldAttachment attachment, string id, string reference) =>
        new(id, attachment.Name, attachment.MediaType, reference, attachment.ScenarioId, attachment.StepIndex);

    internal async Task<ImmutableArray<EvidenceAttachment>> CompleteAsync()
    {
        (EvidenceAttachment Attachment, Task? Write)[] attachments;
        lock (_gate)
        {
            KeepUnsettled();
            attachments = [.. _attachments];
        }
        if (attachments.Length == 0)
        {
            return [];
        }
        ImmutableArray<EvidenceAttachment>.Builder completed = ImmutableArray.CreateBuilder<EvidenceAttachment>(attachments.Length);
        foreach ((EvidenceAttachment attachment, Task? write) in attachments)
        {
            Result written = await Result.TryAsync(() => write ?? Task.CompletedTask, IsUnwritable).ConfigureAwait(false);
            completed.Add(written.Match(() => attachment, _ => attachment with
            {
                Reference = MissingReference,
                Available = false
            }));
        }
        return completed.MoveToImmutable();
    }

    private static bool IsUnwritable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private void KeepUnsettled()
    {
        _held.ForEach(Keep);
        _held.Clear();
    }

    internal static string Extension(string name, string mediaType) =>
        Path.GetExtension(name) is { Length: > 1 and <= 11 } extension && extension.Skip(1).All(char.IsAsciiLetterOrDigit)
            && string.Equals(MediaType(extension), mediaType, StringComparison.OrdinalIgnoreCase)
            ? extension
            : mediaType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                "application/json" => ".json",
                "text/plain" => ".txt",
                _ => ".bin"
            };

    internal static string MediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".csv" => "text/csv",
        ".html" or ".htm" => "text/html",
        ".txt" or ".log" => "text/plain",
        _ => "application/octet-stream"
    };
}

internal sealed record HeldAttachment(string? ScenarioId, int? StepIndex, string Name, string MediaType, string? FilePath, Action<string>? Write);
