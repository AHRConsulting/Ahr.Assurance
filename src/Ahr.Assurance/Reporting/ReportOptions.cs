using System.Collections.Immutable;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

/// <summary>Immutable options for running verifications and delivering their evidence.</summary>
public sealed record ReportOptions
{
    /// <summary>Exports raw operational exception details. Authored assertion evidence is independent.</summary>
    public bool IncludeDiagnosticDetails { get; init; }

    /// <summary>Captured test-output detail. <see cref="AssuranceTextVerbosity.Off"/> disables it.</summary>
    public AssuranceTextVerbosity TextOutput { get; init; } = AssuranceTextVerbosity.Standard;

    /// <summary>
    /// Defaults to JSON evidence; <c>dotnet assurance report</c> builds HTML from it. Add <see cref="ReportSinks.Html"/> for
    /// HTML per verification. An empty list disables delivery, not execution or assertions.
    /// </summary>
    public ImmutableArray<IAssuranceEvidenceSink> Sinks { get; init; } = [ReportSinks.Json()];
    private readonly string? _defaultOutputDirectory;

    /// <summary>Where sinks write. A relative path is resolved against <see cref="AppContext.BaseDirectory"/>.
    /// Defaults to a folder per run inside <c>runs</c> in <c>ahr-assurance.json</c>, or to <c>artifacts/assurance</c>.</summary>
    public string DefaultOutputDirectory
    {
        get => _defaultOutputDirectory ?? AssuranceConfig.Current.Map(config => config.RunsDirectory).GetValueOrDefault("artifacts/assurance");
        init => _defaultOutputDirectory = value;
    }

    /// <summary>Where <see cref="Execution.Attach"/> writes content and what attached file paths are relative to.
    /// Defaults to <c>attachments</c> in <c>ahr-assurance.json</c>, or <c>attachments</c>.
    /// A relative path is resolved against the folder evidence is written to.</summary>
    public string AttachmentsDirectory { get; init; } = AssuranceConfig.Current.Bind(config => config.Attachments.ToOption()).GetValueOrDefault("attachments");

    internal bool UsesRunFolders => _defaultOutputDirectory is null && AssuranceConfig.Current.IsSome;

    /// <summary>The run ID that groups verifications into one run. When <see langword="null"/>, the <c>ASSURANCE_RUN_ID</c>
    /// environment variable is used, or else an ID made once per test process.</summary>
    public string? CorrelationId { get; init; }
    internal string? Producer { get; init; }
    internal ImmutableArray<EvidenceAttachment> Attachments { get; init; } = [];
    internal IAttachmentContentSource? AttachmentSource { get; init; }
    internal Func<CancellationToken, Task<ImmutableArray<EvidenceAttachment>>>? FinalizeAttachmentsAsync { get; init; }
    internal TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

internal sealed record VerificationContext
{
    public string? TestId { get; init; }
    public string? TestName { get; init; }
    public Func<string, CancellationToken, Task>? PublishTextAsync { get; init; }
}
