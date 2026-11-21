using System.Collections.Immutable;
using Ahr.Assurance.Reporting.Html;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class RunReport
{
    private static readonly string _processCorrelation = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";

    internal static (string Id, string Source) Correlation(ReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var environment = Environment.GetEnvironmentVariable("ASSURANCE_RUN_ID");
        return options.CorrelationId is { } id ? (id, "options")
            : environment is not null ? (environment, "environment")
            : (_processCorrelation, "process");
    }

    internal static string Folder(ReportOptions options, string correlation)
    {
        ArgumentNullException.ThrowIfNull(options);
        var directory = Path.GetFullPath(options.DefaultOutputDirectory, AppContext.BaseDirectory);
        return options.UsesRunFolders ? Path.Combine(directory, RunFolders.Name(correlation)) : directory;
    }

    internal static async Task<Option<string>> WriteAsync(ReportOptions options, TestRunSummary summary, string? title, bool writeReport,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        (var correlation, _) = Correlation(options);
        var folder = Folder(options, correlation);
        if (!Directory.Exists(folder) || !RunFolders.IsRun(folder))
        {
            return Option<string>.None;
        }

        AssuranceResults loaded = await AssuranceResults.LoadAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);
        ImmutableArray<VerificationEvidence> run = [.. loaded.Verifications
            .Where(evidence => string.Equals(evidence.CorrelationId, correlation, StringComparison.Ordinal))
            .OrderBy(evidence => evidence.Report.Run.StartedAt)
            .Select(evidence => EvidenceExport.Project(evidence, options.IncludeDiagnosticDetails))];
        if (run.IsEmpty)
        {
            return Option<string>.None;
        }

        await (summary with { CorrelationId = correlation }).WriteAsync(folder, cancellationToken).ConfigureAwait(false);
        ReportFormatContext context = Context(options, loaded, run, folder, correlation, title);
        if (!writeReport || !ShowsAny(run, context))
        {
            return Option<string>.None;
        }

        ReportFormatResult written = await new HtmlReportFormat().WriteAsync(context, cancellationToken).ConfigureAwait(false);
        return written.Path.ToOption();
    }

    private static ReportFormatContext Context(ReportOptions options, AssuranceResults loaded, ImmutableArray<VerificationEvidence> run, string folder,
        string correlation, string? title)
    {
        Option<AssuranceConfig> config = AssuranceConfig.Current;
        return new()
        {
            Inputs = [new ReportFormatInput { Results = new(loaded.Directory, run), Tests = TestRunSummary.Read(folder, [correlation]).GetValueOrDefault() }],
            OutputDirectory = Path.Combine(folder, config.Bind(item => item.ReportOutput.ToOption()).GetValueOrDefault(AssuranceConfig.DefaultReportOutput)),
            Title = string.IsNullOrWhiteSpace(title) ? config.Bind(item => item.DisplayTitle.ToOption()).GetValueOrDefault() : title,
            DefaultTitle = config.Map(item => item.TitleOr(item.Project)).GetValueOrDefault(),
            ExcludedContexts = config.Map(item => item.ExcludedContexts).GetValueOrDefault([]),
            IncludeDiagnosticDetails = options.IncludeDiagnosticDetails
        };
    }

    internal static bool ShowsAny(IEnumerable<VerificationEvidence> run, ReportFormatContext context) =>
        run.Any(evidence => evidence.Report.Run.Scenarios.IsEmpty || evidence.Report.Run.Scenarios.Any(context.Shows));
}
