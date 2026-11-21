using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed record ReportProject(string Name, AssuranceConfig Config);

internal sealed record ReportSolution(string Path, ImmutableArray<ReportProject> Projects, Option<AssuranceConfig> Config)
{
    internal string Title => Config.Map(config => config.TitleOr(FileName)).GetValueOrDefault(FileName);

    private string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);

    internal string SectionTitle(ReportProject project) => SharesConfig(project) ? project.Name : project.Config.TitleOr(project.Name);

    private bool SharesConfig(ReportProject project) => Config.Map(config => config.Path == project.Config.Path).GetValueOrDefault(false);

    internal string DefaultOutput => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "artifacts", "assurance", "report");
}

internal sealed record ReportScope(AssuranceConfig? Config, ReportSolution? Solution);

internal static partial class ReportTarget
{
    internal static Result<ReportScope, CommandError> Resolve(string? target, string? configPath, string[] projects, string[] excludedProjects,
        bool hasInput, string currentDirectory)
    {
        if (configPath is not null)
        {
            return OnlyOption("--config", target, [.. projects, .. excludedProjects])
                .Bind(_ => Load(configPath))
                .Map(config => new ReportScope(config, null));
        }

        if (hasInput)
        {
            return OnlyOption("--input", target, [.. projects, .. excludedProjects])
                .Bind(_ => Nearest(currentDirectory))
                .Map(config => new ReportScope(config.GetValueOrDefault(), null));
        }

        var path = Path.GetFullPath(target ?? currentDirectory, currentDirectory);
        return Located(path, target is not null).Bind(found => found.Solution.Match(
            solution => Solution(solution, projects, excludedProjects).Map(projectsFound => new ReportScope(null, projectsFound)),
            () => WithoutSolution("--project", projects)
                .Bind(_ => WithoutSolution("--exclude-project", excludedProjects))
                .Bind(_ => Nearest(found.Project.Map(project => Path.GetDirectoryName(project)!).GetValueOrDefault(path)))
                .Map(config => new ReportScope(config.GetValueOrDefault(), null))));
    }

    internal static Task<int> RunAsync(Result<ReportScope, CommandError> scope, Func<ReportScope, Task<int>> run, TextWriter error) =>
        scope.MatchAsync(run, async failure =>
        {
            await error.WriteLineAsync($"Assurance report failed [{failure.Code}]: {failure.Message}").ConfigureAwait(false);
            return 1;
        });

    private static Result<(Option<string> Solution, Option<string> Project), CommandError> Located(string path, bool named)
    {
        if (File.Exists(path))
        {
            return FileTarget(path);
        }

        if (Directory.Exists(path))
        {
            return FolderTarget(path);
        }

        return named ? TargetError<(Option<string>, Option<string>)>($"{path} does not exist.") : Found(Option<string>.None, Option<string>.None);
    }

    private static Result<(Option<string> Solution, Option<string> Project), CommandError> FileTarget(string path)
    {
        if (IsSolution(path))
        {
            return Found(Option<string>.Some(path), Option<string>.None);
        }

        if (path.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            return Found(Option<string>.None, Option<string>.Some(path));
        }

        return TargetError<(Option<string>, Option<string>)>($"{path} is not a project, solution or folder.");
    }

    private static Result<(Option<string> Solution, Option<string> Project), CommandError> FolderTarget(string path)
    {
        string[] solutions = [.. Directory.GetFiles(path).Where(IsSolution).Order(StringComparer.Ordinal)];
        var unclear = solutions.Length > 1 || (solutions.Length == 1 && Directory.GetFiles(path, "*.*proj").Length != 0);
        if (unclear)
        {
            return TargetError<(Option<string>, Option<string>)>($"{path} holds more than one project or solution file. Name the one to report.");
        }

        return Found(solutions.SingleOrNone(), Option<string>.None);
    }

    private static Result<(Option<string> Solution, Option<string> Project), CommandError> Found(Option<string> solution, Option<string> project) =>
        CommandResult.Success((solution, project));

    private static Result<string[], CommandError> OnlyOption(string option, string? target, string[] projects) =>
        CommandResult.Success(projects)
            .Ensure(named => target is null && named.Length == 0, new CommandError("target-error", $"Pass {option}, or a project or solution, not both."));

    private static Result<string[], CommandError> WithoutSolution(string option, string[] names) =>
        CommandResult.Success(names)
            .Ensure(named => named.Length == 0, new CommandError("target-error", $"{option} needs a solution. Run report in the solution's folder, or name the solution."));

    private static Result<Option<AssuranceConfig>, CommandError> Nearest(string directory) =>
        AssuranceConfig.Find(directory).Match(
            path => Load(path).Map(Option<AssuranceConfig>.Some),
            () => CommandResult.Success(Option<AssuranceConfig>.None));

    private static Result<AssuranceConfig, CommandError> Load(string path)
    {
        var full = Path.GetFullPath(path);
        return Result.Try(() => File.ReadAllText(full), IsUnreadable, ConfigError)
            .Bind(json => AssuranceConfig.Read(full, json).MapError(error => new CommandError("config-error", $"{full} is not valid: {error.Message}")));
    }

    private static Result<ReportSolution, CommandError> Solution(string path, string[] names, string[] excludedNames)
    {
        var root = Path.GetDirectoryName(path)!;
        var solutionConfig = Path.Combine(root, AssuranceConfig.FileName);
        return SolutionConfig(solutionConfig).Bind(config => SolutionProjects(path, root, solutionConfig, names, excludedNames)
            .Map(projects => new ReportSolution(path, [.. projects], config)));
    }

    private static Result<IReadOnlyList<ReportProject>, CommandError> SolutionProjects(string path, string root, string solutionConfig, string[] names,
        string[] excludedNames) =>
        Result.Try(() => ProjectPaths(path).ToArray(), IsUnreadable, ConfigError)
            .Map(paths => paths
                .Select(project => AssuranceConfig.Find(Path.GetDirectoryName(project)!)
                    .Where(config => config.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    .Where(config => !IsSolutionConfigSeenFromSubfolder(config, solutionConfig, root, project))
                    .Map(config => (Name: Path.GetFileNameWithoutExtension(project), Config: config)))
                .Values()
                .ToArray())
            .Bind(found => Known(path, found, [.. names, .. excludedNames]))
            .Bind(found => found
                .Where(item => names.Length == 0 || names.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                .Where(item => !excludedNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                .GroupBy(item => item.Config, StringComparer.Ordinal)
                .Traverse(group => Load(group.Key).Map(config =>
                    new ReportProject(group.Count() == 1 ? group.First().Name : Path.GetRelativePath(root, Path.GetDirectoryName(group.Key)!), config))))
            .Ensure(projects => projects.Count != 0, new CommandError("target-error",
                $"No project in {path} has {AssuranceConfig.FileName}. Run 'dotnet assurance init' in each test project's folder."));

    private static bool IsSolutionConfigSeenFromSubfolder(string config, string solutionConfig, string root, string project) =>
        string.Equals(config, solutionConfig, StringComparison.Ordinal) && !string.Equals(Path.GetDirectoryName(project), root, StringComparison.Ordinal);

    private static Result<Option<AssuranceConfig>, CommandError> SolutionConfig(string path) =>
        File.Exists(path) ? Load(path).Map(Option<AssuranceConfig>.Some) : CommandResult.Success(Option<AssuranceConfig>.None);

    private static Result<(string Name, string Config)[], CommandError> Known(string path, (string Name, string Config)[] found, string[] named)
    {
        string[] unknown = [.. named.Where(name => !found.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))];
        return CommandResult.Success(found).Ensure(_ => unknown.Length == 0, new CommandError("target-error",
            $"No project '{string.Join("', '", unknown)}' with {AssuranceConfig.FileName} in {path}. Available: {CommandText.ListOrNone(found.Select(item => item.Name))}."));
    }

    private static Result<T, CommandError> TargetError<T>(string message) => CommandResult.Failure<T>("target-error", message);

    private static CommandError ConfigError(Exception exception) => new("config-error", exception.Message);

    private static bool IsUnreadable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or System.Xml.XmlException;

    internal static IEnumerable<string> ProjectPaths(string solution)
    {
        var root = Path.GetDirectoryName(solution)!;
        return Listed(solution).Where(item => item.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.GetFullPath(item.Replace('\\', '/'), root));
    }

    private static IEnumerable<string> Listed(string solution)
    {
        if (solution.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return XDocument.Load(solution).Descendants("Project").Select(element => (string?)element.Attribute("Path")).OfType<string>();
        }
        return SlnProject().Matches(File.ReadAllText(solution)).Select(match => match.Groups[1].Value);
    }

    internal static bool IsSolution(string path) =>
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(""""^Project\("[^"]*"\)\s*=\s*"[^"]*"\s*,\s*"([^"]+)"""", RegexOptions.Multiline)]
    private static partial Regex SlnProject();
}
