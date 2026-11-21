using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

internal sealed class SolutionFolder : IDisposable
{
    public string Root { get; } = TestReports.TemporaryRoot();

    public string In(params string[] parts) => Path.Combine([Root, .. parts]);

    public CommandError Error { get; set; } = null!;

    public IReadOnlyList<string> SlnxProjects { get; set; } = [];

    public IReadOnlyList<string> SlnProjects { get; set; } = [];

    public void Holds(params string[] projects)
    {
        foreach (var project in projects)
        {
            var folder = Directory.CreateDirectory(In("tests", project)).FullName;
            File.WriteAllText(Path.Combine(folder, project + ".csproj"), "<Project />");
            if (project.EndsWith(".Tests", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(folder, AssuranceConfig.FileName), "{}");
            }
        }
        File.WriteAllText(In("All.slnx"),
            $"<Solution>{string.Concat(projects.Select(project => $"<Project Path=\"tests/{project}/{project}.csproj\" />"))}</Solution>");
    }

    public void Dispose() => Directory.Delete(Root, true);
}

public sealed class ChoosingWhatToReport
{
    [Fact]
    public Task SolutionFilesListTheirProjects() =>
        Scenario.Define<SolutionFolder>("Both solution formats list their projects, in folders or not", CliBehaviour.SolutionReports)
            .Given("An .slnx with a project in a folder and one outside, and an .sln with one project", folder =>
            {
                File.WriteAllText(folder.In("All.slnx"), """
                    <Solution>
                      <Folder Name="/tests/">
                        <Project Path="tests/Checkout.Tests/Checkout.Tests.csproj" />
                      </Folder>
                      <Project Path="src/App/App.fsproj" />
                    </Solution>
                    """);
                File.WriteAllText(folder.In("All.sln"), """
                    Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{11111111-1111-1111-1111-111111111111}"
                    EndProject
                    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Checkout.Tests", "tests\Checkout.Tests\Checkout.Tests.csproj", "{22222222-2222-2222-2222-222222222222}"
                    EndProject
                    """);
            })
            .When("Each solution's projects are listed", folder =>
                (folder.SlnxProjects, folder.SlnProjects) = ([.. ReportTarget.ProjectPaths(folder.In("All.slnx"))], [.. ReportTarget.ProjectPaths(folder.In("All.sln"))]))
            .Then("The .slnx lists both projects", folder => Assert.Equal(
                [folder.In("tests", "Checkout.Tests", "Checkout.Tests.csproj"), folder.In("src", "App", "App.fsproj")], folder.SlnxProjects))
            .And("The .sln lists its project and not its solution folder", folder => Assert.Equal(
                [folder.In("tests", "Checkout.Tests", "Checkout.Tests.csproj")], folder.SlnProjects))
            .VerifyAsync();

    [Theory]
    [InlineData("both", null, null, false, "holds more than one project or solution file")]
    [InlineData(null, "App", null, false, "No project 'App' with ahr-assurance.json")]
    [InlineData(null, null, "App", false, "No project 'App' with ahr-assurance.json")]
    [InlineData("tests/Checkout.Tests", "Checkout.Tests", null, false, "--project needs a solution")]
    [InlineData("tests/Checkout.Tests", null, "Checkout.Tests", false, "--exclude-project needs a solution")]
    [InlineData("All.slnx", null, null, true, "Pass --input, or a project or solution, not both")]
    [InlineData("missing", null, null, false, "does not exist")]
    public Task UnclearTargetsAreRefused(string? target, string? project, string? excludedProject, bool input, string message) =>
        Scenario.Define<SolutionFolder>("A target the CLI can't settle on is refused, saying why", CliBehaviour.SolutionReports,
                description: "The report is never of a guess.")
            .Given("A solution with Checkout.Tests, which has a config, and App, which doesn't, beside a folder holding two project files", folder =>
            {
                folder.Holds("Checkout.Tests", "App");
                var both = Directory.CreateDirectory(folder.In("both")).FullName;
                File.WriteAllText(Path.Combine(both, "Both.sln"), "");
                File.WriteAllText(Path.Combine(both, "Both.csproj"), "<Project />");
            })
            .When(Describe(target, project, excludedProject, input), folder => folder.Error = ReportTarget.Resolve(target, null,
                project is null ? [] : [project], excludedProject is null ? [] : [excludedProject], input, folder.Root).Error)
            .Then("It is refused as a target error", folder => Assert.Equal("target-error", folder.Error.Code))
            .And($"The error says {message}", folder => Assert.Contains(message, folder.Error.Message))
            .VerifyAsync();

    private static string Describe(string? target, string? project, string? excludedProject, bool input)
    {
        List<string> parts = [target is null ? "No target" : $"The target '{target}'"];
        if (project is not null)
        {
            parts.Add($"--project {project}");
        }
        if (excludedProject is not null)
        {
            parts.Add($"--exclude-project {excludedProject}");
        }
        if (input)
        {
            parts.Add("--input");
        }
        return string.Join(", with ", parts) + " is asked for";
    }
}
