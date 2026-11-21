using System.Collections.Immutable;
using System.Xml;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed record InitChoice(Option<TestFramework> Framework, bool Playwright)
{
    internal bool AddsPackages => Framework.IsSome || Playwright;
}

internal sealed record ProjectSetup(ProjectFrameworks Project, ImmutableArray<TestFramework> Adds);

internal sealed record InitPlan(ImmutableArray<ProjectSetup> Projects, ImmutableArray<string> Skipped, Option<string> ConfigOnly);

internal sealed record InitHint(string Text, bool HasAdapter);

internal sealed class InitCommand(IPackageReferences packages, TextWriter output, TextWriter error)
{
    internal int Run(string directory, bool force = false, string[]? frameworks = null)
    {
        directory = Path.GetFullPath(directory);
        try
        {
            return Choice(frameworks ?? [])
                .Bind(choice => Plan(directory, force, choice))
                .Bind(Validated)
                .Match(Apply, Failed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or XmlException)
        {
            error.WriteLine($"Assurance init failed: {exception.Message}");
            return 1;
        }
    }

    private static Result<InitChoice, CommandError> Choice(string[] values)
    {
        string[] names = [.. values.SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))];
        string[] unknown = [.. names.Where(name => TestFramework.Named(name).IsNone)];
        if (unknown.Length != 0)
        {
            return Failure<InitChoice>($"'{string.Join("', '", unknown)}' is not a framework init knows. Use one of: {TestFramework.Names}.");
        }
        TestFramework[] named = [.. names.Select(name => TestFramework.Named(name).Value).Distinct()];
        TestFramework[] tests = [.. named.Where(framework => framework != TestFramework.Playwright)];
        if (tests.Length > 1)
        {
            return Failure<InitChoice>($"Name one test framework, not {Listed(tests)}. Playwright can be added beside it.");
        }
        return CommandResult.Success(new InitChoice(tests.FirstOrNone(), named.Contains(TestFramework.Playwright)));
    }

    private static Result<InitPlan, CommandError> Plan(string directory, bool force, InitChoice choice)
    {
        string[] projects = [.. Directory.GetFiles(directory, "*.*proj").Order(StringComparer.Ordinal)];
        string[] solutions = [.. Directory.GetFiles(directory).Where(ReportTarget.IsSolution).Order(StringComparer.Ordinal)];
        if (projects.Length > 1)
        {
            return Failure<InitPlan>($"{directory} holds more than one project file. Run init in a folder with one, or in a solution's folder.");
        }
        if (projects.Length == 1)
        {
            return Project(ProjectFrameworks.Read(projects[0]), choice).Map(setup => new InitPlan([setup], [], Option<string>.None));
        }
        if (solutions.Length > 1)
        {
            return Failure<InitPlan>($"{directory} holds more than one solution file. Run init in the folder of the one to set up.");
        }
        if (solutions.Length == 1)
        {
            return Solution(solutions[0], choice);
        }
        if (force && !choice.AddsPackages)
        {
            return CommandResult.Success(new InitPlan([], [], Option<string>.Some(directory)));
        }
        return Failure<InitPlan>($"{directory} has no project file. Run init in a test project's folder or a solution's folder, or pass --force to create the file here.");
    }

    private static Result<ProjectSetup, CommandError> Project(ProjectFrameworks project, InitChoice choice) =>
        choice.Framework.Match(framework => Framework(project, framework), () => CommandResult.Success(Option<TestFramework>.None))
            .Bind(framework => Playwright(project, choice, framework)
                .Map(playwright => new ProjectSetup(project, [.. new[] { framework, playwright }.Values()])));

    private static Result<Option<TestFramework>, CommandError> Framework(ProjectFrameworks project, TestFramework framework)
    {
        if (project.Adapter.IsSome)
        {
            return CommandResult.Success(Option<TestFramework>.None)
                .Ensure(_ => project.Adapter.Value == framework, new CommandError("init-error",
                    $"{project.Name} already references {project.Adapter.Value.Adapter}, not {framework.Adapter}. Nothing changed."));
        }
        if (project.Frameworks.Length != 0 && !project.Frameworks.Contains(framework))
        {
            return Failure<Option<TestFramework>>($"{project.Name} uses {Listed(project.Frameworks)}, not {framework.Display}. Nothing changed.");
        }
        return CommandResult.Success(Option<TestFramework>.Some(framework));
    }

    private static Result<Option<TestFramework>, CommandError> Playwright(ProjectFrameworks project, InitChoice choice, Option<TestFramework> framework)
    {
        if (!choice.Playwright || project.HasPlaywright)
        {
            return CommandResult.Success(Option<TestFramework>.None);
        }
        if (project.Adapter.IsNone && framework.IsNone)
        {
            return Failure<Option<TestFramework>>($"{project.Name} has no Assurance adapter for Playwright to work beside. " +
                "Name its test framework too, such as 'dotnet assurance init --framework xunit-v3,playwright'. Nothing changed.");
        }
        return CommandResult.Success(Option<TestFramework>.Some(TestFramework.Playwright));
    }

    private static Result<InitPlan, CommandError> Solution(string solution, InitChoice choice)
    {
        ProjectFrameworks[] projects = [.. ReportTarget.ProjectPaths(solution).Where(File.Exists).Select(ProjectFrameworks.Read)];
        SolutionSelection selection = Selection(choice);
        ProjectFrameworks[] matching = [.. projects.Where(selection.Included)];
        if (matching.Length == 0)
        {
            return Failure<InitPlan>($"No project in {solution} {selection.NoneFound}. Nothing changed.");
        }
        string[] skipped = [.. projects.Where(project => !selection.Included(project)).Select(project => $"{project.Name}: {selection.Reason(project)}")];
        return matching.Traverse(project => Project(project, choice))
            .Map(setups => new InitPlan([.. setups], [.. skipped], Option<string>.None));
    }

    private static SolutionSelection Selection(InitChoice choice)
    {
        if (choice.Framework.IsSome)
        {
            TestFramework framework = choice.Framework.Value;
            return new(project => Uses(project, framework), Uses, $"uses {framework.Display}");
        }
        if (choice.Playwright)
        {
            return new(project => project.UsesPlaywright || project.HasPlaywright, _ => "doesn't use Microsoft.Playwright", "uses Microsoft.Playwright");
        }
        return new(project => project.IsTestProject, _ => "no test framework", $"references a test framework init knows: {Listed(TestFramework.All)}");
    }

    private sealed record SolutionSelection(Func<ProjectFrameworks, bool> Included, Func<ProjectFrameworks, string> Reason, string NoneFound);

    private static bool Uses(ProjectFrameworks project, TestFramework framework) =>
        project.Adapter.Match(adapter => adapter == framework, () => project.Frameworks.Contains(framework));

    private static string Uses(ProjectFrameworks project)
    {
        if (!project.IsTestProject)
        {
            return "no test framework";
        }
        return "uses " + project.Adapter.Match(adapter => adapter.Display, () => Listed(project.Frameworks));
    }

    private static Result<InitPlan, CommandError> Validated(InitPlan plan)
    {
        foreach (var folder in plan.Projects.Select(setup => setup.Project.Folder).Concat(plan.ConfigOnly.Match(folder => new[] { folder }, () => [])))
        {
            var path = Path.Combine(folder, AssuranceConfig.FileName);
            if (File.Exists(path))
            {
                _ = AssuranceConfig.Load(path);
            }
        }
        return CommandResult.Success(plan);
    }

    private int Apply(InitPlan plan)
    {
        foreach (var skipped in plan.Skipped)
        {
            output.WriteLine($"Skipped {skipped}.");
        }
        var changed = false;
        var ready = true;
        foreach (ProjectSetup setup in plan.Projects)
        {
            Result<bool, CommandError> added = AddPackages(setup);
            if (added.IsFailure)
            {
                return Failed(added.Error);
            }
            changed |= WriteConfig(setup.Project.Folder, added.Value) | added.Value;
            ready &= Hint(setup).Match(Said, () => true);
        }
        changed |= plan.ConfigOnly.Match(folder => WriteConfig(folder, false), () => false);
        if (changed && ready)
        {
            output.WriteLine("Run the tests, then 'dotnet assurance report'.");
        }
        return 0;
    }

    private Result<bool, CommandError> AddPackages(ProjectSetup setup)
    {
        foreach (TestFramework package in setup.Adds)
        {
            Result<string, CommandError> added = packages.Add(setup.Project.Project, package.Adapter, AssuranceVersion.Current);
            if (added.IsFailure)
            {
                return CommandResult.Failure<bool>(added.Error with
                {
                    Message = $"{added.Error.Message} No {AssuranceConfig.FileName} was written for {setup.Project.Name}. " +
                        $"Check the package source, or add {package.Adapter} {AssuranceVersion.Current} yourself and run init again."
                });
            }
            output.WriteLine($"Added {package.Adapter} {AssuranceVersion.Current} to {setup.Project.Name}, for {package.Display}.");
        }
        foreach (var adapter in Kept(setup.Project))
        {
            output.WriteLine($"{setup.Project.Name} already references {adapter}.");
        }
        return CommandResult.Success(setup.Adds.Length != 0);
    }

    private static IEnumerable<string> Kept(ProjectFrameworks project)
    {
        if (project.Adapter.IsSome)
        {
            yield return project.Adapter.Value.Adapter;
        }
        if (project.HasPlaywright)
        {
            yield return TestFramework.Playwright.Adapter;
        }
    }

    private static Option<InitHint> Hint(ProjectSetup setup)
    {
        ProjectFrameworks project = setup.Project;
        var playwright = project.UsesPlaywright && !project.HasPlaywright;
        if (setup.Adds.Length != 0)
        {
            return Option<InitHint>.None;
        }
        if (project.Adapter.IsSome && playwright)
        {
            return Option<InitHint>.Some(new($"{project.Name} uses Microsoft.Playwright. To add a screenshot of each step: dotnet assurance init --framework playwright", true));
        }
        if (project.Adapter.IsSome)
        {
            return Option<InitHint>.None;
        }
        if (project.Frameworks.Length == 1)
        {
            TestFramework framework = project.Frameworks[0];
            var names = playwright ? $"{framework.Name},playwright" : framework.Name;
            return Option<InitHint>.Some(new($"{project.Name} uses {framework.Display}. To add its adapter: dotnet assurance init --framework {names}", false));
        }
        if (project.Frameworks.Length > 1)
        {
            return Option<InitHint>.Some(new($"{project.Name} could use more than one adapter: {Listed(project.Frameworks)}. " +
                "To add the one it runs on: dotnet assurance init --framework <name>", false));
        }
        return Option<InitHint>.Some(new($"{project.Name} references no test framework init knows. To add the adapter for yours: " +
            $"dotnet assurance init --framework <name>, with one of: {Listed(TestFramework.All)}", false));
    }

    private bool Said(InitHint hint)
    {
        output.WriteLine(hint.Text);
        return hint.HasAdapter;
    }

    private bool WriteConfig(string folder, bool projectChanged)
    {
        var path = Path.Combine(folder, AssuranceConfig.FileName);
        if (File.Exists(path))
        {
            output.WriteLine(projectChanged ? $"{path} already exists." : $"{path} already exists. Nothing changed.");
            return false;
        }
        using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write))
        using (StreamWriter writer = new(stream))
        {
            writer.Write(AssuranceConfig.Template);
        }
        output.WriteLine($"Created {path}");
        return true;
    }

    private int Failed(CommandError failure)
    {
        error.WriteLine($"Assurance init failed: {failure.Message}");
        if (failure.Detail is { Length: > 0 } detail)
        {
            error.WriteLine(detail);
        }
        return 1;
    }

    private static string Listed(IEnumerable<TestFramework> frameworks) => string.Join(", ", frameworks.Select(framework => framework.Described));

    private static Result<T, CommandError> Failure<T>(string message) => CommandResult.Failure<T>("init-error", message);
}
