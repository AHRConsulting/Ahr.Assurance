using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed class StandardReportWriter : IRunReportWriter
{
    private readonly Func<string, Result<IReportFormat, CommandError>> _formats;

    internal StandardReportWriter(Func<string, Result<IReportFormat, CommandError>>? formats = null) =>
        _formats = formats ?? HtmlOnly;

    internal StandardReportWriter(IReadOnlyList<IReportFormat> formats) : this(name => Named(formats, name))
    {
    }

    internal StandardReportWriter(IReportFormat format) : this(_ => CommandResult.Success(format))
    {
    }

    public Task<Result<string, CommandError>> WriteAsync(SelectedRun run, CancellationToken token) =>
        _formats(run.Format).BindAsync(format => WriteThroughAsync(format, run, token)).BindAsync(Delivered);

    private static Result<IReportFormat, CommandError> Named(IReadOnlyList<IReportFormat> formats, string name)
    {
        if (formats.FirstOrDefault(format => format.Name == name) is { } found)
        {
            return CommandResult.Success(found);
        }
        return CommandResult.Failure<IReportFormat>("unknown-format",
            $"No format '{name}'. This CLI writes {string.Join(", ", formats.Select(format => format.Name))}.");
    }

    private static Result<IReportFormat, CommandError> HtmlOnly(string name)
    {
        if (name == "html")
        {
            return CommandResult.Success<IReportFormat>(new HtmlReportFormat());
        }
        return CommandResult.Failure<IReportFormat>("unknown-format", $"No format '{name}'.");
    }

    private static Task<Result<ReportFormatResult, CommandError>> WriteThroughAsync(IReportFormat format, SelectedRun run, CancellationToken token) =>
        Result.TryAsync(async () =>
            {
                _ = Directory.CreateDirectory(run.OutputDirectory);
                return await format.WriteAsync(Context(run), token).ConfigureAwait(false);
            },
            exception => ReportApplication.Error("report-write-error", "The selected-run report could not be fully delivered.", exception));

    private static Result<string, CommandError> Delivered(ReportFormatResult written)
    {
        if (written.Diagnostics.IsDefaultOrEmpty)
        {
            return CommandResult.Success(written.Path);
        }
        return CommandResult.Failure<string>("report-delivery-error", "Some report outputs could not be delivered. A partial report may be available.",
            string.Join(Environment.NewLine, written.Diagnostics.Select(item => item.Message)));
    }

    internal static ReportFormatContext Context(SelectedRun run) => new()
    {
        Inputs = [.. run.Verifications.GroupBy(item => item.Directory, StringComparer.Ordinal).Select(folder => Input(run, folder))],
        OutputDirectory = run.OutputDirectory,
        Title = run.Title,
        DefaultTitle = run.DefaultTitle,
        Contexts = run.Contexts.IsDefault ? [] : run.Contexts,
        ExcludedContexts = run.ExcludedContexts.IsDefault ? [] : run.ExcludedContexts,
        Developer = run.Developer,
        IncludeDiagnosticDetails = run.IncludeDiagnosticDetails
    };

    private static ReportFormatInput Input(SelectedRun run, IGrouping<string, ReportInput> folder) => new()
    {
        Results = new(folder.Key, [.. folder.OrderBy(item => item.Evidence.Report.Run.StartedAt)
            .Select(item => EvidenceExport.Project(item.Evidence, run.IncludeDiagnosticDetails))], run.AttachmentsDirectory),
        Tests = TestRunSummary.Read(folder.Key, folder.Select(item => item.Evidence.CorrelationId)).GetValueOrDefault(),
        Project = ProjectTitle(run, folder.Key)
    };

    private static string? ProjectTitle(SelectedRun run, string folder)
    {
        if (run.Projects.IsDefaultOrEmpty)
        {
            return null;
        }
        return run.Projects.First(project => string.Equals(project.Folder, folder, StringComparison.Ordinal)).Title;
    }
}
