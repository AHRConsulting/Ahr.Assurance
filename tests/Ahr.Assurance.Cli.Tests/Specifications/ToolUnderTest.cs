using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli.Tests.Specifications;

internal sealed record ReportCall
{
    public string Folder { get; init; } = "";
    public string? Target { get; init; }
    public string[] Projects { get; init; } = [];
    public string[] ExcludedProjects { get; init; } = [];
    public string[] Inputs { get; init; } = [];
    public string? Run { get; init; }
    public string? Output { get; init; }
    public string? Title { get; init; }
    public string[]? Contexts { get; init; }
    public string[]? ExcludedContexts { get; init; }
    public string[]? Formats { get; init; }
}

/// <summary>The CLI's commands, run in the test's process in a folder of their own, as a team runs them in a terminal.</summary>
public sealed class ToolUnderTest : IToolRun, IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public string Root { get; } = TestReports.TemporaryRoot();

    public int ExitCode { get; private set; } = -1;

    /// <summary>What the CLI said since the scenario's setup finished.</summary>
    public string Said => _output.ToString() + _error;

    internal IReportFormat? Format { get; set; }

    public string PathOf(string relative) => Path.GetFullPath(relative, Root);

    public string Read(string relative) => File.ReadAllText(PathOf(relative));

    /// <summary>Forgets what the CLI said while the scenario was set up.</summary>
    public void Forget()
    {
        _ = _output.GetStringBuilder().Clear();
        _ = _error.GetStringBuilder().Clear();
    }

    internal async Task ReportAsync(ReportCall call, CancellationToken token)
    {
        string[] inputs = [.. call.Inputs.Select(PathOf)];
        Result<ReportScope, CommandError> scope = ReportTarget.Resolve(call.Target, null, call.Projects, call.ExcludedProjects, inputs.Length != 0, PathOf(call.Folder));
        if (scope.IsFailure)
        {
            _error.WriteLine($"Assurance report failed [{scope.Error.Code}]: {scope.Error.Message}");
            ExitCode = 1;
            return;
        }
        (AssuranceConfig? config, ReportSolution? solution) = scope.Value;
        StandardReportWriter writer = Format is null ? new([new HtmlReportFormat()]) : new(Format);
        ExitCode = await new ReportCommand(new(new ReportFiles(), writer), _output, _error).RunAsync(new(
            inputs, call.Output is null ? null : PathOf(call.Output), call.Run, false,
            Title: call.Title, Contexts: call.Contexts, Config: config, Solution: solution, Formats: call.Formats,
            ExcludedContexts: call.ExcludedContexts), token);
    }

    internal FakePackages Packages { get; } = new();

    internal void Init(string folder, bool force = false, params string[] frameworks) =>
        ExitCode = new InitCommand(Packages, _output, _error).Run(PathOf(folder), force, frameworks);

    /// <summary>A project file at <paramref name="path"/> that references each package in <paramref name="packages"/>, written as <c>Id</c> or <c>Id/Version</c>.</summary>
    public void ProjectFile(string path, string sdk, params string[] packages)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(PathOf(path))!);
        var references = string.Concat(packages.Select(Reference));
        File.WriteAllText(PathOf(path), $"<Project Sdk=\"{sdk}\"><ItemGroup>{references}</ItemGroup></Project>");
    }

    private static string Reference(string package)
    {
        var parts = package.Split('/', 2);
        if (parts.Length == 1)
        {
            return $"<PackageReference Include=\"{parts[0]}\" />";
        }
        return $"<PackageReference Include=\"{parts[0]}\" Version=\"{parts[1]}\" />";
    }

    /// <summary>A test project's folder, with its project file and, unless <paramref name="config"/> is null, its config file.</summary>
    public void Project(string folder, string? config = "{}")
    {
        var path = Directory.CreateDirectory(PathOf(folder)).FullName;
        File.WriteAllText(Path.Combine(path, Path.GetFileName(path) + ".csproj"), "<Project />");
        if (config is not null)
        {
            File.WriteAllText(Path.Combine(path, AssuranceConfig.FileName), config);
        }
    }

    /// <summary>All.slnx, listing each project under <c>tests/</c>. Names ending in <c>.Tests</c> get a config file.</summary>
    public void Solution(params string[] projects)
    {
        foreach (var project in projects)
        {
            Project($"tests/{project}", project.EndsWith(".Tests", StringComparison.Ordinal) ? "{}" : null);
        }
        File.WriteAllText(PathOf("All.slnx"),
            $"<Solution>{string.Concat(projects.Select(project => $"<Project Path=\"tests/{project}/{project}.csproj\" />"))}</Solution>");
    }

    /// <summary>Writes a verification's evidence into <paramref name="folder"/>, as a test run writes it.</summary>
    public Task SaveAsync(string folder, VerificationEvidence evidence, CancellationToken token, string file = "a")
    {
        var path = Directory.CreateDirectory(PathOf(folder)).FullName;
        return File.WriteAllTextAsync(Path.Combine(path, file + ".json"), EvidenceDocumentJson.Serialize(evidence), token);
    }

    /// <summary>Evidence of one verification in run <paramref name="run"/>, recorded by this version of Ahr.Assurance.</summary>
    public static VerificationEvidence Evidence(string run, DateTimeOffset started, params ScenarioResult[] journeys) =>
        new(Guid.NewGuid().ToString("N"), run, "producer", "test", "Test", VerificationExecutionState.Completed,
            new(new("Journey", started, TimeSpan.Zero, [.. journeys]), []), [])
        {
            AssuranceVersion = AssuranceVersion.Current
        };

    /// <summary>A passing scenario in the business context with ID <paramref name="id"/> and name <paramref name="area"/>.</summary>
    public static ScenarioResult Journey(string name, string id, string area) =>
        Passing(name, BusinessContext.Define(id, area, area + " purpose").Capability(id + "-c", "Capability").Outcome(id + "-o", "Outcome").Build());

    /// <summary>A passing scenario, in the business context named <paramref name="area"/> when there is one.</summary>
    public static ScenarioResult Journey(string name, string? area = null) =>
        area is null ? Passing(name, null) : Journey(name, area, area);

    private static ScenarioResult Passing(string name, BusinessContext? business) =>
        new(name.Replace(' ', '-'), name, business, [new("Satisfied", StepKind.Then, new(AssuranceStatus.Satisfied, "Observed"), TimeSpan.Zero)]);

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class FakePackages : IPackageReferences
{
    public const string FailureDetail = "error: Unable to find package.";

    public List<(string Project, string Package, string Version)> Added { get; } = [];

    public string? Failure { get; set; }

    public string? FailingPackage { get; set; }

    public Result<string, CommandError> Add(string project, string package, string version)
    {
        if (Fails(package))
        {
            return CommandResult.Failure<string>("package-error", Failure!, FailureDetail);
        }
        Added.Add((Path.GetFileName(project), package, version));
        var text = File.ReadAllText(project).Replace("</Project>", $"<ItemGroup><PackageReference Include=\"{package}\" Version=\"{version}\" /></ItemGroup></Project>",
            StringComparison.Ordinal);
        File.WriteAllText(project, text);
        return CommandResult.Success("dotnet add package");
    }

    private bool Fails(string package) => Failure is not null && (FailingPackage is null || FailingPackage == package);
}

public sealed class ToolSteps : ScenarioSteps<ToolUnderTest>
{
    internal static DateTimeOffset Day(int day) => DateTimeOffset.UnixEpoch.AddDays(day - 1);

    public static ScenarioAction<ToolUnderTest> TheReportRunsIn(string folder) =>
        CreateActionStep($"'dotnet assurance report' runs in {folder}", (tool, token) => tool.ReportAsync(new() { Folder = folder }, token));

    public static ScenarioAction<ToolUnderTest> TheSolutionsConfigHolds(string json) =>
        CreateActionStep($"The config beside the solution holds {json}", tool => File.WriteAllText(tool.PathOf(AssuranceConfig.FileName), json));

    public static ScenarioAction<ToolUnderTest> AProjectWithTwoRuns { get; } =
        CreateActionStep("Checkout.Tests has run 'zz-older' from 1 January and run 'aa-newer' from 2 January, so their names sort the other way",
            async (tool, token) =>
            {
                tool.Project("Checkout.Tests", """{ "report": { "title": "Release 0.1" } }""");
                await tool.SaveAsync("Checkout.Tests/artifacts/assurance/zz-older", ToolUnderTest.Evidence("zz-older", Day(1), ToolUnderTest.Journey("Older journey")), token);
                await tool.SaveAsync("Checkout.Tests/artifacts/assurance/aa-newer", ToolUnderTest.Evidence("aa-newer", Day(2), ToolUnderTest.Journey("Newer journey")), token);
            });

    public static ScenarioAction<ToolUnderTest> ASolutionWithRuns { get; } =
        CreateActionStep("All.slnx lists Checkout.Tests, Billing.Tests titled 'Billing', Search.Tests and App, which has no config; " +
            "Checkout.Tests ran 'old' and then 'ci-7', Billing.Tests ran 'ci-7' and then 'late', and Search.Tests hasn't run", async (tool, token) =>
            {
                tool.Solution("Checkout.Tests", "Billing.Tests", "Search.Tests", "App");
                File.WriteAllText(tool.PathOf($"tests/Billing.Tests/{AssuranceConfig.FileName}"), """{ "report": { "title": "Billing" } }""");
                DateTimeOffset now = Day(11);
                await tool.SaveAsync(Runs("Checkout.Tests", "old"), ToolUnderTest.Evidence("old", now.AddDays(-1), ToolUnderTest.Journey("Old checkout")), token);
                await tool.SaveAsync(Runs("Checkout.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", now, ToolUnderTest.Journey("Pays by card")), token);
                await tool.SaveAsync(Runs("Billing.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", now.AddMinutes(2), ToolUnderTest.Journey("Sends the invoice")), token);
                await tool.SaveAsync(Runs("Billing.Tests", "late"), ToolUnderTest.Evidence("late", now.AddHours(3), ToolUnderTest.Journey("Late billing")), token);
            });

    public static ScenarioAction<ToolUnderTest> PaymentsWithTwoAreas { get; } =
        CreateActionStep("Payments.Tests ran 'Pays by card' in the checkout area and 'Finds a product' in the search area", async (tool, token) =>
        {
            tool.Project("Payments.Tests");
            await tool.SaveAsync("Payments.Tests/artifacts/assurance/run", ToolUnderTest.Evidence("run", Day(1),
                ToolUnderTest.Journey("Pays by card", "checkout"), ToolUnderTest.Journey("Finds a product", "search")), token);
        });

    public static ScenarioAction<ToolUnderTest> PaymentsLeavingOutSearch { get; } =
        CreateActionStep("Payments.Tests, whose config leaves out the search area, ran 'Pays by card' in checkout and 'Finds a product' in search",
            async (tool, token) =>
            {
                tool.Project("Payments.Tests", """{ "report": { "excludeContexts": [ "search" ] } }""");
                await tool.SaveAsync("Payments.Tests/artifacts/assurance/run", ToolUnderTest.Evidence("run", Day(1),
                    ToolUnderTest.Journey("Pays by card", "checkout"), ToolUnderTest.Journey("Finds a product", "search")), token);
            });

    public static ScenarioAction<ToolUnderTest> TheReportLeavesOut(params string[] contexts) =>
        CreateActionStep($"'dotnet assurance report{string.Concat(contexts.Select(context => " --exclude-context " + context))}' runs in Payments.Tests",
            (tool, token) => tool.ReportAsync(new() { Folder = "Payments.Tests", ExcludedContexts = contexts }, token));

    public static ScenarioAction<ToolUnderTest> ACheckoutRun { get; } =
        CreateActionStep("Checkout.Tests has the template config and one run", (tool, token) =>
        {
            tool.Project("Checkout.Tests", AssuranceConfig.Template);
            return tool.SaveAsync("Checkout.Tests/artifacts/assurance/run", ToolUnderTest.Evidence("run", Day(1), ToolUnderTest.Journey("Journey")), token);
        });

    public static ScenarioAction<ToolUnderTest> InitRuns { get; } =
        CreateActionStep("'dotnet assurance init' runs in the folder", tool => tool.Init("."));

    public static ScenarioExpectation<ToolUnderTest> TheConfigHolds(string json) =>
        CreateAssertionStep($"ahr-assurance.json holds {json}", tool => Assert.Equal(json, tool.Read(AssuranceConfig.FileName)));

    internal static string Runs(string project, string run) => $"tests/{project}/artifacts/assurance/{run}";
}
