using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed record ReportRequest(string[] Inputs, string? OutputDirectory, string? CorrelationId, bool Overwrite, bool IncludeDiagnosticDetails = false, bool Developer = false, string? Title = null, string[]? Contexts = null, string? AttachmentsDirectory = null, AssuranceConfig? Config = null, ReportSolution? Solution = null, string[]? Formats = null, string[]? ExcludedContexts = null);
internal sealed record ReportInput(VerificationEvidence Evidence, string Directory);
internal sealed record SelectedRun(string CorrelationId, ImmutableArray<ReportInput> Verifications, string OutputDirectory, bool IncludeDiagnosticDetails, bool Developer = false, string? Title = null, ImmutableArray<string> Contexts = default, string? AttachmentsDirectory = null, ImmutableArray<RunProject> Projects = default, string? DefaultTitle = null, string Format = "html", ImmutableArray<string> ExcludedContexts = default);
internal sealed record CommandError(string Code, string Message, string? Detail = null);
internal sealed record ReportCompletion(string IndexPath, int Count, string CorrelationId, int ExitCode, ImmutableArray<string> Lines = default)
{
    internal ImmutableArray<(string Format, string Path)> Outputs { get; init; } = [];

    internal ImmutableArray<CommandError> FormatErrors { get; init; } = [];

    internal TestRunSummary? Tests { get; init; }
}
internal sealed record RunProject(string Title, string Folder);
internal sealed record ProjectChoice(Option<RunProject> Run, string Line);
internal sealed record StartedRun(string Folder, DateTimeOffset Started);
internal sealed record PickedRun(string Folder, string Note);
internal sealed record ChosenRuns(ReportRequest Request, ImmutableArray<string> Lines, ImmutableArray<RunProject> Projects = default, string? DefaultTitle = null);

internal interface IReportFiles
{
    string FullPath(string path);
    bool DirectoryExists(string path);
    string[] JsonFiles(string directory);
    string[] Directories(string directory);
    bool IsEmpty(string directory);
    Task<string> ReadAsync(string path, CancellationToken token);
}

internal sealed class ReportFiles : IReportFiles
{
    public string FullPath(string path) => Path.GetFullPath(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string[] JsonFiles(string directory) => Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
    public string[] Directories(string directory) => Directory.GetDirectories(directory);
    public bool IsEmpty(string directory) => !Directory.EnumerateFileSystemEntries(directory).Any();
    public Task<string> ReadAsync(string path, CancellationToken token) => File.ReadAllTextAsync(path, token);
}

internal interface IRunReportWriter
{
    Task<Result<string, CommandError>> WriteAsync(SelectedRun run, CancellationToken token);
}

internal sealed class ReportApplication(IReportFiles files, IRunReportWriter writer)
{
    internal const string DefaultTitle = "Delivery report";
    internal const string DefaultOutput = "assurance-report";

    internal async Task<Result<ReportCompletion, CommandError>> RunAsync(ReportRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Result<ChosenRuns, CommandError> chosen = await ChooseAsync(request, token).ConfigureAwait(false);
        return await chosen.BindAsync(choice => RunAsync(choice.Request, choice, token)).ConfigureAwait(false);
    }

    private Task<Result<ChosenRuns, CommandError>> ChooseAsync(ReportRequest request, CancellationToken token)
    {
        if (request.Inputs.Length == 0 && request.Solution is { } solution)
        {
            return ChooseSolutionAsync(request, solution, token);
        }
        return ChooseRunAsync(request, token);
    }

    private async Task<Result<ReportCompletion, CommandError>> RunAsync(ReportRequest request, ChosenRuns chosen, CancellationToken token)
    {
        var resolved = Result.Try(() => Resolve(request), IsPathFailure,
            exception => Error("input-error", "Evidence input could not be resolved.", exception));
        return await resolved.BindAsync(async selection =>
        {
            if (selection.Paths.Length == 0)
            {
                return CommandResult.Failure<ReportCompletion>("no-evidence", "No JSON evidence was found in the selected inputs.");
            }
            Result<IReadOnlyList<ReportInput>, CommandError> loaded = await selection.Paths.TraverseAsync(path => ReadInputAsync(path, token)).ConfigureAwait(false);
            Result<SelectedRun, CommandError> selected = loaded.Bind(items => Select([.. items], selection.Output, request))
                .Map(run => run with { Projects = chosen.Projects, DefaultTitle = chosen.DefaultTitle });
            return await selected.BindAsync(run => WriteFormatsAsync(run, request, chosen, token)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private Task<Result<ReportInput, CommandError>> ReadInputAsync(string path, CancellationToken token) =>
        ReadEvidenceAsync(path, "An evidence file could not be read or validated.", token)
            .MapAsync(evidence => new ReportInput(evidence, Path.GetDirectoryName(path)!));

    private async Task<Result<VerificationEvidence, CommandError>> ReadEvidenceAsync(string path, string message, CancellationToken token)
    {
        Result<string, CommandError> text = await Result.TryAsync(cancellation => files.ReadAsync(path, cancellation), IsPathFailure,
            exception => Error("evidence-read-error", message, exception), token).ConfigureAwait(false);
        return text.Bind(json => EvidenceDocumentJson.Read(json).MapError(error => new CommandError("evidence-read-error", message, error.Message)));
    }

    private async Task<Result<ReportCompletion, CommandError>> WriteFormatsAsync(SelectedRun run, ReportRequest request, ChosenRuns chosen, CancellationToken token)
    {
        string[] formats = request.Formats is { Length: > 0 } named ? [.. named.Distinct(StringComparer.Ordinal)] : ["html"];
        (IReadOnlyList<(string Format, string Path)> outputs, IReadOnlyList<CommandError> errors) =
            await formats.PartitionAsync(format => WriteFormatAsync(run, request, format, token)).ConfigureAwait(false);
        if (outputs.Count == 0)
        {
            return CommandResult.Failure<ReportCompletion>(errors[0]);
        }
        ImmutableArray<string> lines = [.. chosen.Lines, .. Warnings(run.Verifications)];
        return CommandResult.Success(new ReportCompletion(outputs[0].Path, run.Verifications.Length, run.CorrelationId, ExitCode(run, errors), lines)
        {
            Outputs = [.. outputs],
            FormatErrors = [.. errors],
            Tests = Tests(run.Verifications)
        });
    }

    private Task<Result<(string Format, string Path), CommandError>> WriteFormatAsync(SelectedRun run, ReportRequest request, string format, CancellationToken token)
    {
        var output = format == "html" ? run.OutputDirectory : run.OutputDirectory + "-" + format;
        Result<bool, CommandError> writable = format == "html" ? CommandResult.Success(true) : ValidateOutput(run.Verifications, output, request);
        return writable
            .BindAsync(_ => writer.WriteAsync(run with { OutputDirectory = output, Format = format }, token))
            .MapAsync(path => (format, path))
            .MapErrorAsync(error => error with { Message = $"Format '{format}': {error.Message}" });
    }

    private static int ExitCode(SelectedRun run, IReadOnlyList<CommandError> errors)
    {
        if (errors.Count != 0)
        {
            return 1;
        }
        return run.Verifications.Any(IsUnsatisfied) ? 2 : 0;
    }

    private static bool IsUnsatisfied(ReportInput item) =>
        item.Evidence.ExecutionState != VerificationExecutionState.Completed ||
        item.Evidence.Report.Run.Status != AssuranceStatus.Satisfied ||
        !item.Evidence.Report.Diagnostics.IsEmpty;

    private static IEnumerable<string> Warnings(ImmutableArray<ReportInput> verifications)
    {
        if (OtherVersionsWarning(verifications) is { } versions)
        {
            yield return versions;
        }
        if (SharedScenarioIdsWarning(verifications) is { } shared)
        {
            yield return shared;
        }
    }

    private static string? OtherVersionsWarning(ImmutableArray<ReportInput> verifications)
    {
        string[] versions = [.. verifications.Select(item => item.Evidence.AssuranceVersion).OfType<string>()
            .Where(version => version != AssuranceVersion.Current).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (versions.Length == 0)
        {
            return null;
        }
        return $"Warning: this evidence was recorded by Ahr.Assurance {string.Join(", ", versions)}, and this CLI is {AssuranceVersion.Current}. " +
            "Use the same version for the packages and the CLI.";
    }

    private static string? SharedScenarioIdsWarning(ImmutableArray<ReportInput> verifications)
    {
        var shared = SharedScenarioIds(verifications);
        if (shared.Length == 0)
        {
            return null;
        }
        var subject = shared.Length == 1 ? " is" : "s are";
        var more = shared.Length > 5 ? ", …" : "";
        return $"Warning: {shared.Length} scenario ID{subject} used by more than one test, " +
            $"so the report cannot tell them apart: '{string.Join("', '", shared.Take(5))}'{more}. Give each scenario a distinct name or ID.";
    }

    private static TestRunSummary? Tests(ImmutableArray<ReportInput> verifications) =>
        verifications.GroupBy(item => item.Directory, StringComparer.Ordinal)
            .Select(folder => TestRunSummary.Read(folder.Key, folder.Select(item => item.Evidence.CorrelationId)).ToResult(new Error("A run folder has no summary.")))
            .Sequence()
            .Ensure(summaries => summaries.Count != 0, "No run folder.")
            .Map(TestRunSummary.Combine)
            .ToOption()
            .GetValueOrDefault();

    private static string[] SharedScenarioIds(ImmutableArray<ReportInput> verifications) =>
        [.. verifications
            .SelectMany(item => item.Evidence.Report.Run.Scenarios.Select(scenario => (item.Evidence.CorrelationId, item.Evidence.Producer, item.Evidence.TestId, scenario.Id)))
            .GroupBy(item => (item.CorrelationId, item.Producer, item.Id))
            .Where(group => group.Select(item => item.TestId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => group.Key.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private (string Output, string[] Paths) Resolve(ReportRequest request)
    {
        var output = files.FullPath(request.OutputDirectory ?? DefaultOutput);
        List<string> paths = [];
        foreach (var input in request.Inputs)
        {
            var path = files.FullPath(input);
            paths.AddRange(files.DirectoryExists(path) ? files.JsonFiles(path).Order(StringComparer.Ordinal) : [path]);
        }
        return (output, [.. paths.Distinct(StringComparer.Ordinal)]);
    }

    private async Task<Result<ChosenRuns, CommandError>> ChooseRunAsync(ReportRequest request, CancellationToken token)
    {
        if (request.Inputs.Length != 0)
        {
            return CommandResult.Success(new ChosenRuns(request, []));
        }
        if (request.Config is not { } config)
        {
            return CommandResult.Failure<ChosenRuns>("no-input",
                $"No evidence selected. Pass --input, or run 'dotnet assurance init' in the test project's folder to create {AssuranceConfig.FileName}.");
        }
        Result<PickedRun, CommandError> picked = await RunsOf(config)
            .BindAsync(listed => PickRunAsync(config, request.CorrelationId, listed, token)).ConfigureAwait(false);
        return picked.Map(run => RunFolderChoice(request, config, run));
    }

    private async Task<Result<PickedRun, CommandError>> PickRunAsync(AssuranceConfig config, string? id, (string Runs, string[] Folders) listed, CancellationToken token)
    {
        (var runs, var folders) = listed;
        if (id is not null)
        {
            return Named(runs, folders, id);
        }
        if (folders.Length == 0)
        {
            return CommandResult.Failure<PickedRun>("no-evidence", $"No runs found in {runs}, the runs folder in {config.Path}. Run the tests first.");
        }
        Result<StartedRun, CommandError> newest = await NewestAsync(folders, token).ConfigureAwait(false);
        var note = folders.Length == 1 ? "the only run" : $"newest of {folders.Length} runs; pass --run to choose another";
        return newest.Map(run => new PickedRun(run.Folder, note));
    }

    private static ChosenRuns RunFolderChoice(ReportRequest request, AssuranceConfig config, PickedRun run) => new(request with
    {
        Inputs = [run.Folder],
        OutputDirectory = request.OutputDirectory ?? Path.GetFullPath(config.ReportOutput ?? AssuranceConfig.DefaultReportOutput, run.Folder),
        Overwrite = request.Overwrite || request.OutputDirectory is null
    }, [$"Run folder: {run.Folder} ({run.Note})"], DefaultTitle: config.TitleOr(config.Project));

    private async Task<Result<ChosenRuns, CommandError>> ChooseSolutionAsync(ReportRequest request, ReportSolution solution, CancellationToken token)
    {
        Result<IReadOnlyList<ProjectChoice>, CommandError> picked =
            await solution.Projects.TraverseAsync(project => ChooseProjectAsync(request, solution, project, token)).ConfigureAwait(false);
        return picked.Bind(projects => SolutionChoice(request, solution, projects));
    }

    private static Result<ChosenRuns, CommandError> SolutionChoice(ReportRequest request, ReportSolution solution, IReadOnlyList<ProjectChoice> projects)
    {
        RunProject[] chosen = [.. projects.Select(project => project.Run).Values()];
        if (chosen.Length == 0)
        {
            return CommandResult.Failure<ChosenRuns>(NoProjectRun(solution, request.CorrelationId));
        }
        ReportRequest rewritten = request with
        {
            Inputs = [.. chosen.Select(project => project.Folder)],
            OutputDirectory = request.OutputDirectory ?? solution.DefaultOutput,
            Overwrite = request.Overwrite || request.OutputDirectory is null
        };
        return CommandResult.Success(new ChosenRuns(rewritten, [$"Solution: {solution.Path}", .. projects.Select(project => project.Line)], [.. chosen],
            solution.Title));
    }

    private static CommandError NoProjectRun(ReportSolution solution, string? id)
    {
        if (id is null)
        {
            return new("no-evidence", $"No runs found for any project in {solution.Path}. Run the tests first.");
        }
        return new("unknown-run", $"No project in {solution.Path} has run '{id}'.");
    }

    private Task<Result<ProjectChoice, CommandError>> ChooseProjectAsync(ReportRequest request, ReportSolution solution, ReportProject project,
        CancellationToken token) =>
        RunsOf(project.Config).BindAsync(async listed =>
        {
            (var runs, var folders) = listed;
            var named = NamedRunFolder(runs, request.CorrelationId);
            if (!HasRun(folders, named))
            {
                return LeftOut(project, runs, request.CorrelationId);
            }

            Result<StartedRun, CommandError> run = await FindRunAsync(folders, named, token).ConfigureAwait(false);
            return run.Map(found => Included(solution.SectionTitle(project), project, found));
        });

    private static string? NamedRunFolder(string runs, string? id) => id is null ? null : Path.Combine(runs, RunFolders.Name(id));

    private static bool HasRun(string[] folders, string? named) =>
        folders.Length != 0 && (named is null || folders.Contains(named, StringComparer.Ordinal));

    private Task<Result<StartedRun, CommandError>> FindRunAsync(string[] folders, string? named, CancellationToken token) =>
        named is null ? NewestAsync(folders, token) : StartedAsync(named, token);

    private static Result<ProjectChoice, CommandError> LeftOut(ReportProject project, string runs, string? id)
    {
        var missing = id is null ? "no runs" : $"no run '{id}'";
        return CommandResult.Success(new ProjectChoice(Option<RunProject>.None, $"Project {project.Name}: {missing} in {runs}, left out of the report"));
    }

    private static ProjectChoice Included(string title, ReportProject project, StartedRun run) =>
        new(Option<RunProject>.Some(new(title, run.Folder)),
            $"Project {project.Name}: {run.Folder} (started {run.Started.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC)");

    private Result<(string Runs, string[] Folders), CommandError> RunsOf(AssuranceConfig config)
    {
        var runs = files.FullPath(config.RunsDirectory);
        return Result.Try(() => (runs, EvidenceFolders(runs)), IsPathFailure, exception => Error("input-error", "The runs folder could not be read.", exception));
    }

    private string[] EvidenceFolders(string runs)
    {
        if (!files.DirectoryExists(runs))
        {
            return [];
        }
        return [.. files.Directories(runs).Where(folder => files.JsonFiles(folder).Length != 0).Order(StringComparer.Ordinal)];
    }

    private static Result<PickedRun, CommandError> Named(string runs, string[] folders, string id)
    {
        var folder = Path.Combine(runs, RunFolders.Name(id));
        if (folders.Contains(folder, StringComparer.Ordinal))
        {
            return CommandResult.Success(new PickedRun(folder, "chosen with --run"));
        }
        return CommandResult.Failure<PickedRun>("unknown-run", $"No run '{id}' in {runs}. Available: {CommandText.ListOrNone(folders.Select(item => Path.GetFileName(item)))}.");
    }

    private async Task<Result<StartedRun, CommandError>> NewestAsync(string[] folders, CancellationToken token)
    {
        Result<IReadOnlyList<StartedRun>, CommandError> runs =
            await folders.TraverseAsync(folder => StartedAsync(folder, token)).ConfigureAwait(false);
        return runs.Map(all => all.Aggregate((newest, run) => run.Started >= newest.Started ? run : newest));
    }

    private async Task<Result<StartedRun, CommandError>> StartedAsync(string folder, CancellationToken token)
    {
        var path = files.JsonFiles(folder).Order(StringComparer.Ordinal).First();
        Result<VerificationEvidence, CommandError> read = await ReadEvidenceAsync(path, $"An evidence file could not be read or validated: {path}", token)
            .ConfigureAwait(false);
        return read.Map(evidence => new StartedRun(folder, evidence.Report.Run.StartedAt));
    }

    private Result<SelectedRun, CommandError> Select(ImmutableArray<ReportInput> inputs, string output, ReportRequest request)
    {
        string[] correlations = [.. inputs.Select(item => item.Evidence.CorrelationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        var combined = request.Solution is not null;
        if (!combined && request.CorrelationId is null && correlations.Length != 1)
        {
            return CommandResult.Failure<SelectedRun>("ambiguous-run", "Several runs found. Select exactly one with --run <id>.");
        }
        var correlation = combined ? string.Join(", ", correlations) : request.CorrelationId ?? correlations[0];
        ImmutableArray<ReportInput> selected = combined ? inputs : [.. inputs.Where(item => item.Evidence.CorrelationId == correlation)];
        if (selected.IsEmpty)
        {
            return CommandResult.Failure<SelectedRun>("unknown-run", "No verification evidence matches the selected run.");
        }
        string[] available = [.. selected.SelectMany(item => item.Evidence.Report.Run.Scenarios)
            .Select(scenario => scenario.Business?.Id).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        string[] missing = [.. (request.Contexts ?? []).Concat(request.ExcludedContexts ?? []).Where(context => !available.Contains(context, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];
        if (missing.Length != 0)
        {
            return CommandResult.Failure<SelectedRun>("unknown-context",
                $"No evidence in the selected run has business context '{string.Join("', '", missing)}'. Available: {CommandText.ListOrNone(available)}.");
        }
        string[] both = [.. (request.Contexts ?? []).Intersect(request.ExcludedContexts ?? [], StringComparer.Ordinal)];
        if (both.Length != 0)
        {
            return CommandResult.Failure<SelectedRun>("context-conflict",
                $"Business context '{string.Join("', '", both)}' is both shown with --context and left out with --exclude-context.");
        }
        ImmutableArray<string> excluded = Excluded(request);
        if (!RunReport.ShowsAny(selected.Select(item => item.Evidence), new() { Inputs = [], OutputDirectory = output, Contexts = [.. request.Contexts ?? []], ExcludedContexts = excluded }))
        {
            return CommandResult.Failure<SelectedRun>("nothing-to-report",
                $"Every journey in the selected run is in a business context left out of the report: {string.Join(", ", excluded)}.");
        }
        if (HasDuplicateIdentities(selected))
        {
            return CommandResult.Failure<SelectedRun>("duplicate-evidence", "Duplicate verification identities in the selected run.");
        }
        if (ContextConflict(selected, combined) is { } conflict)
        {
            return CommandResult.Failure<SelectedRun>("context-conflict", conflict);
        }
        return ValidateOutput(inputs, output, request).Map(_ => SelectedRunOf(correlation, selected, output, request));
    }

    private static string? ContextConflict(ImmutableArray<ReportInput> selected, bool solution)
    {
        IEnumerable<ReportInput[]> scopes = solution ? selected.GroupBy(item => item.Directory, StringComparer.Ordinal).Select(scope => scope.ToArray()) : [[.. selected]];
        foreach (ReportInput[] scope in scopes)
        {
            if (Conflict(scope) is { } message)
            {
                return solution ? $"{message} In {scope[0].Directory}." : message;
            }
        }
        return null;
    }

    private static string? Conflict(ReportInput[] scope)
    {
        AssuranceRunResult run = new("Contexts", DateTimeOffset.UnixEpoch, TimeSpan.Zero, [.. scope.SelectMany(item => item.Evidence.Report.Run.Scenarios)]);
        return BusinessReportGrouping.Read(run).Match<string?>(_ => null, error => error.Message);
    }

    private static bool HasDuplicateIdentities(ImmutableArray<ReportInput> selected) =>
        selected.Select(item => item.Evidence.VerificationId).Distinct(StringComparer.Ordinal).Count() != selected.Length;

    private SelectedRun SelectedRunOf(string correlation, ImmutableArray<ReportInput> selected, string output, ReportRequest request) =>
        new(correlation, selected, output, request.IncludeDiagnosticDetails,
            request.Developer || request.IncludeDiagnosticDetails,
            string.IsNullOrWhiteSpace(request.Title) ? request.Config?.DisplayTitle : request.Title,
            [.. (request.Contexts ?? []).Distinct(StringComparer.Ordinal)],
            request.AttachmentsDirectory is null ? null : files.FullPath(request.AttachmentsDirectory))
        {
            ExcludedContexts = Excluded(request)
        };

    internal static ImmutableArray<string> Excluded(ReportRequest request)
    {
        if (request.ExcludedContexts is { Length: > 0 } named)
        {
            return [.. named.Distinct(StringComparer.Ordinal)];
        }
        if (request.Contexts is { Length: > 0 })
        {
            return [];
        }
        IEnumerable<AssuranceConfig> configs = request.Solution?.Projects.Select(project => project.Config) ?? (request.Config is { } config ? [config] : []);
        return [.. configs.SelectMany(config => config.ExcludedContexts).Distinct(StringComparer.Ordinal)];
    }

    private Result<bool, CommandError> ValidateOutput(ImmutableArray<ReportInput> inputs, string output, ReportRequest request)
    {
        if (IsAnInput(inputs, output))
        {
            return CommandResult.Failure<bool>("input-output-conflict", "Output must differ from an input directory.");
        }
        return Result.Try(() => request.Overwrite || !files.DirectoryExists(output) || files.IsEmpty(output), IsPathFailure,
                exception => Error("output-error", "Report destination could not be inspected.", exception))
            .Ensure(allowed => allowed, new("output-not-empty", "Output directory is not empty; choose another directory or pass --overwrite."));
    }

    private static bool IsAnInput(ImmutableArray<ReportInput> inputs, string output) =>
        inputs.Any(item => string.Equals(item.Directory, output, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    internal static CommandError Error(string code, string message, Exception exception) => new(code, message, exception.Message);

    private static bool IsPathFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException;
}
