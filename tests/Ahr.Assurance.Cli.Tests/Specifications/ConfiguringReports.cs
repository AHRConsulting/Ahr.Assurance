using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

internal sealed class ConfigFile
{
    public string Path { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project", AssuranceConfig.FileName);

    public string Json { get; set; } = "{}";

    public AssuranceConfig Config { get; set; } = null!;

    public string Folder => System.IO.Path.GetDirectoryName(Path)!;
}

internal sealed class ConfigSteps : ScenarioSteps<ConfigFile>
{
    public static ScenarioAction<ConfigFile> ItIsRead { get; } = CreateActionStep("It is read", file =>
        file.Config = AssuranceConfig.Parse(file.Path, file.Json));
}

public sealed class ConfiguringReports
{
    [Fact]
    public Task EverySettingIsReadAsWritten() =>
        Scenario.Define<ConfigFile>("Every setting is read as written, with comments and trailing commas allowed", CliBehaviour.Configuration)
            .Given("A file that sets runs, attachments, the report and keep, with a comment, a trailing comma and an unknown key", file => file.Json = """
                {
                  // Comments and trailing commas are allowed.
                  "runs": "out/runs",
                  "attachments": "../shared/attachments",
                  "report": { "output": "html", "title": "Release 0.1" },
                  "keep": 3,
                  "unknown": true,
                }
                """)
            .When(ConfigSteps.ItIsRead)
            .Then("Runs are relative to the file", file => Assert.Equal(Path.Combine(file.Folder, "out", "runs"), file.Config.RunsDirectory))
            .And("Attachments, the report folder and its title are kept as written", file =>
            {
                Assert.Equal("../shared/attachments", file.Config.Attachments);
                Assert.Equal("html", file.Config.ReportOutput);
                Assert.Equal("Release 0.1", file.Config.Title);
            })
            .And("Three runs are kept", file => Assert.Equal(3, file.Config.Keep))
            .VerifyAsync();

    [Fact]
    public Task AnEmptyFileUsesTheDefaults() =>
        Scenario.Define<ConfigFile>("An empty file keeps runs in artifacts/assurance and sets nothing else", CliBehaviour.Configuration)
            .Given("A file holding {}", file => file.Json = "{}")
            .When(ConfigSteps.ItIsRead)
            .Then("Runs go to artifacts/assurance beside the file", file =>
                Assert.Equal(Path.Combine(file.Folder, "artifacts", "assurance"), file.Config.RunsDirectory))
            .And("Nothing else is set", file =>
            {
                Assert.Null(file.Config.Attachments);
                Assert.Null(file.Config.ReportOutput);
                Assert.Null(file.Config.Title);
                Assert.Null(file.Config.Version);
                Assert.Null(file.Config.Keep);
                Assert.Empty(file.Config.ExcludedContexts);
            })
            .VerifyAsync();

    [Fact]
    public Task TheTemplateWritesEveryDefault() =>
        Scenario.Define<ConfigFile>("The file init writes sets every default and shows the optional settings", CliBehaviour.Configuration)
            .Given("The file init writes", file => (file.Path, file.Json) = ("/project/ahr-assurance.json", AssuranceConfig.Template))
            .When(ConfigSteps.ItIsRead)
            .Then("It sets the defaults", file =>
            {
                Assert.Equal(Path.GetFullPath("/project/artifacts/assurance"), file.Config.RunsDirectory);
                Assert.Equal("attachments", file.Config.Attachments);
                Assert.Equal("report", file.Config.ReportOutput);
                Assert.Null(file.Config.Title);
                Assert.Null(file.Config.Keep);
                Assert.Empty(file.Config.ExcludedContexts);
            })
            .When("Its commented keep, title, version and excluded contexts are uncommented", file => file.Json = AssuranceConfig.Template
                .Replace("// \"keep\": 20,", "\"keep\": 20,", StringComparison.Ordinal)
                .Replace("// \"title\": \"Checkout\",", "\"title\": \"Checkout\",", StringComparison.Ordinal)
                .Replace("// \"version\": \"4.2\",", "\"version\": \"4.2\",", StringComparison.Ordinal)
                .Replace("// \"excludeContexts\"", "\"excludeContexts\"", StringComparison.Ordinal))
            .And(ConfigSteps.ItIsRead)
            .Then("Each one is read", file =>
            {
                Assert.Equal(["technical"], file.Config.ExcludedContexts);
                Assert.Equal(20, file.Config.Keep);
                Assert.Equal("Checkout", file.Config.Title);
                Assert.Equal("4.2", file.Config.Version);
            })
            .VerifyAsync();

    [Fact]
    public Task TheReportIsNamedWithItsVersion() =>
        Scenario.Define<ConfigFile>("A file can name the report and give the version shown after the title", CliBehaviour.Configuration)
            .Given("A file that names the report 'AHR Assurance' at version 0.1.0", file =>
                file.Json = """{ "report": { "title": "AHR Assurance", "version": "0.1.0" } }""")
            .When(ConfigSteps.ItIsRead)
            .Then("The title and the version are read apart", file =>
            {
                Assert.Equal("AHR Assurance", file.Config.Title);
                Assert.Equal("0.1.0", file.Config.Version);
            })
            .And("The report's title is 'AHR Assurance 0.1.0'", file => Assert.Equal("AHR Assurance 0.1.0", file.Config.DisplayTitle))
            .VerifyAsync();

    [Fact]
    public Task AVersionAloneLeavesTheTitleToTheProject() =>
        Scenario.Define<ConfigFile>("A version with no title leaves the title to the project's name", CliBehaviour.Configuration)
            .Given("A file that gives version 2.0 and no title", file => file.Json = """{ "report": { "version": "2.0" } }""")
            .When(ConfigSteps.ItIsRead)
            .Then("The file names no title of its own", file => Assert.Null(file.Config.DisplayTitle))
            .VerifyAsync();

    [Theory]
    [InlineData("[]", "must hold a JSON object")]
    [InlineData("""{ "runs": 5 }""", "'runs' must be a non-empty string")]
    [InlineData("""{ "runs": " " }""", "'runs' must be a non-empty string")]
    [InlineData("""{ "report": "out" }""", "'report' must be an object")]
    [InlineData("""{ "report": { "title": false } }""", "'report.title' must be a non-empty string")]
    [InlineData("""{ "report": { "version": 1 } }""", "'report.version' must be a non-empty string")]
    [InlineData("""{ "keep": 0 }""", "'keep' must be a whole number of at least 1")]
    [InlineData("""{ "keep": 2.5 }""", "'keep' must be a whole number of at least 1")]
    [InlineData("""{ "report": { "excludeContexts": "technical" } }""", "'report.excludeContexts' must be a list of business context IDs")]
    [InlineData("""{ "report": { "excludeContexts": [ "" ] } }""", "'report.excludeContexts' must be a list of business context IDs")]
    [InlineData("{", "is not valid")]
    public Task AnInvalidFileNamesTheFileAndTheProblem(string json, string message) =>
        Scenario.Define<ConfigFile>("An invalid file is refused with its path and the problem", CliBehaviour.Configuration)
            .Given("A file holding " + json, file => (file.Path, file.Json) = ("/repo/ahr-assurance.json", json))
            .WhenThrows<InvalidOperationException>("It is read", file => AssuranceConfig.Parse(file.Path, file.Json))
            .Then("The error names the file", (ConfigFile _, InvalidOperationException error) => Assert.Contains("/repo/ahr-assurance.json", error.Message))
            .And($"The error says {message}", (ConfigFile _, InvalidOperationException error) => Assert.Contains(message, error.Message))
            .VerifyAsync();

    [Fact]
    public Task TheReportCanLeaveOutBusinessContexts() =>
        Scenario.Define<ConfigFile>("The report can leave out business contexts, each named once", CliBehaviour.Configuration)
            .Given("A file that excludes technical, perf and technical again", file =>
                file.Json = """{ "report": { "excludeContexts": [ "technical", "perf", "technical" ] } }""")
            .When(ConfigSteps.ItIsRead)
            .Then("technical and perf are left out", file => Assert.Equal(["technical", "perf"], file.Config.ExcludedContexts))
            .VerifyAsync();

    [Theory]
    [InlineData("""{ "report": { "afterRun": false } }""", false)]
    [InlineData("""{ "report": { "afterRun": true } }""", true)]
    [InlineData("""{ "report": { } }""", false)]
    [InlineData("{}", false)]
    public Task TheReportAfterARunIsOffUnlessTurnedOn(string json, bool afterRun) =>
        Scenario.Define<ConfigFile>("The report is written as the test run ends only when the file turns it on", CliBehaviour.Configuration)
            .Given("A file holding " + json, file => file.Json = json)
            .When(ConfigSteps.ItIsRead)
            .Then(afterRun ? "The report is written after the run" : "The report is not written after the run", file => Assert.Equal(afterRun, file.Config.ReportAfterRun))
            .VerifyAsync();

    [Fact]
    public Task AnAfterRunThatIsNotTrueOrFalseIsRefused() =>
        Scenario.Define<ConfigFile>("An afterRun that isn't true or false is refused, naming the setting", CliBehaviour.Configuration)
            .Given("A file whose report.afterRun is \"no\"", file => file.Json = """{ "report": { "afterRun": "no" } }""")
            .WhenThrows<InvalidOperationException>("It is read", file => AssuranceConfig.Parse(file.Path, file.Json))
            .Then("The error names report.afterRun", (ConfigFile _, InvalidOperationException error) => Assert.Contains("report.afterRun", error.Message))
            .VerifyAsync();

    [Fact]
    public Task TheNearestFileInThisOrAParentFolderIsFound() =>
        Scenario.Define<ProjectFolders>("The nearest ahr-assurance.json in this folder or a parent is used", CliBehaviour.Configuration)
            .Given("A project folder inside a repository with no config", _ => { })
            .Then("No file is found", folders => Assert.True(AssuranceConfig.Find(folders.Project).IsNone))
            .When("The repository gets a config", folders => File.WriteAllText(folders.ConfigIn(folders.Repository), "{}"))
            .Then("The repository's is found from the project folder", folders =>
                Assert.Equal(folders.ConfigIn(folders.Repository), AssuranceConfig.Find(folders.Project).Value))
            .When("The project folder gets its own", folders => File.WriteAllText(folders.ConfigIn(folders.Project), "{}"))
            .Then("The project's own is found", folders => Assert.Equal(folders.ConfigIn(folders.Project), AssuranceConfig.Find(folders.Project).Value))
            .VerifyAsync();

    [Theory]
    [InlineData("20261103T213241Z-81b6", "20261103T213241Z-81b6")]
    [InlineData("release-4.2", "release-4.2")]
    [InlineData("feature/login #7", "feature_login__7")]
    [InlineData("..", "_")]
    public Task RunIdsBecomeFolderNames(string id, string name) =>
        Scenario.Define("A run ID becomes a folder name that is safe on every system", CliBehaviour.Configuration)
            .Given($"The run ID '{id}'", () => { })
            .When("Its folder is named", () => RunFolders.Name(id))
            .Then($"The folder is '{name}'", (string folder) => Assert.Equal(name, folder))
            .VerifyAsync();
}

internal sealed class ProjectFolders : IDisposable
{
    public string Repository { get; } = TestReports.TemporaryRoot();

    public string Project => Directory.CreateDirectory(Path.Combine(Repository, "tests", "Checkout")).FullName;

    public string ConfigIn(string folder) => Path.Combine(folder, AssuranceConfig.FileName);

    public void Dispose() => Directory.Delete(Repository, true);
}
