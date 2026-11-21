using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

public sealed class ReportingASolution
{
    private const string SolutionReport = "artifacts/assurance/report/report.html";

    [Fact]
    public Task ASolutionReportShowsTheNewestRunOfEachProject() =>
        Scenario.Define<ToolUnderTest>("Beside a solution, the report shows the newest run of each configured project in a section of its own",
                CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("The CLI names the solution and the run it chose in each project", tool =>
            {
                Assert.Contains($"Solution: {tool.PathOf("All.slnx")}", tool.Said);
                Assert.Contains($"Project Checkout.Tests: {tool.PathOf(ToolSteps.Runs("Checkout.Tests", "ci-7"))}", tool.Said);
                Assert.Contains($"Project Billing.Tests: {tool.PathOf(ToolSteps.Runs("Billing.Tests", "late"))}", tool.Said);
            })
            .And(ReportSteps.TheCliSays("Project Search.Tests: no runs in"))
            .And(ReportSteps.TheCliSays("left out of the report"))
            .And(ReportSteps.TheCliDoesNotSay("Project App"))
            .And(ReportSteps.TheReportIsWrittenTo(SolutionReport))
            .And(ReportSteps.TheReportIsTitled("All"))
            .And(ReportSteps.TheReportShows("2 projects. Report generated "))
            .And(ReportSteps.TheReportShows("All 2 projects passed their checks."))
            .And(ReportSteps.TheReportShowsInOrder("<h2>Checkout.Tests</h2><p class=\"meta\">Tested 11 January 1970, 00:00 UTC</p>", "Pays by card"))
            .And(ReportSteps.TheReportShowsInOrder("Pays by card", "<h2>Billing</h2><p class=\"meta\">Tested 11 January 1970, 03:00 UTC</p>"))
            .And(ReportSteps.TheReportShowsInOrder("<h2>Billing</h2>", "Late billing"))
            .And(ReportSteps.TheReportDoesNotShow("Old checkout"))
            .And(ReportSteps.TheReportDoesNotShow("Search.Tests"))
            .VerifyAsync();

    [Fact]
    public Task TheConfigBesideTheSolutionNamesTheReport() =>
        Scenario.Define<ToolUnderTest>("The config beside a solution names its report, with the version, below 'Delivery report'",
                CliBehaviour.SolutionReports, description: "Each project in a subfolder keeps its own config, and one without a config is still left out.")
            .Given(ToolSteps.ASolutionWithRuns)
            .And(ToolSteps.TheSolutionsConfigHolds("""{ "report": { "title": "AHR Assurance", "version": "0.1.0" } }"""))
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled("AHR Assurance 0.1.0"))
            .And(ReportSteps.TheReportShows("<header><p class=\"kind\">Delivery report</p><h1>AHR Assurance 0.1.0</h1>"))
            .And(ReportSteps.TheReportShows("<h2>Billing</h2>"))
            .And(ReportSteps.TheCliDoesNotSay("Project App"))
            .VerifyAsync();

    [Fact]
    public Task AVersionBesideTheSolutionFollowsItsName() =>
        Scenario.Define<ToolUnderTest>("A version with no title beside a solution follows the solution's name", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .And(ToolSteps.TheSolutionsConfigHolds("""{ "report": { "version": "0.1.0" } }"""))
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled("All 0.1.0"))
            .VerifyAsync();

    [Fact]
    public Task ATitleGivenReplacesTheSolutionsTitleAndVersion() =>
        Scenario.Define<ToolUnderTest>("A title given on the command line replaces the title and version beside the solution", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .And(ToolSteps.TheSolutionsConfigHolds("""{ "report": { "title": "AHR Assurance", "version": "0.1.0" } }"""))
            .When("'dotnet assurance report --title Nightly' runs beside the solution", (tool, token) => tool.ReportAsync(new() { Title = "Nightly" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled("Nightly"))
            .And(ReportSteps.TheReportDoesNotShow("<h1>AHR Assurance"))
            .VerifyAsync();

    [Fact]
    public Task AnInvalidConfigBesideTheSolutionFailsTheReport() =>
        Scenario.Define<ToolUnderTest>("An invalid config beside a solution fails the report, naming the problem", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .And(ToolSteps.TheSolutionsConfigHolds("""{ "report": { "version": 1 } }"""))
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[config-error]"))
            .And(ReportSteps.TheCliSays("'report.version' must be a non-empty string"))
            .VerifyAsync();

    [Fact]
    public Task AProjectsSectionCarriesItsVersion() =>
        Scenario.Define<ToolUnderTest>("A project's section in a solution report carries the version its config gives", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .And("Billing.Tests's config names it 'Billing' at version 4.2", tool =>
                File.WriteAllText(tool.PathOf($"tests/Billing.Tests/{AssuranceConfig.FileName}"), """{ "report": { "title": "Billing", "version": "4.2" } }"""))
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("<h2>Billing 4.2</h2>"))
            .VerifyAsync();

    [Fact]
    public Task AProjectBesideTheSolutionSharesItsConfig() =>
        Scenario.Define<ToolUnderTest>("A project in the solution's own folder uses the config beside the solution, and its section takes the project's name",
                CliBehaviour.SolutionReports)
            .Given("Checkout.slnx and Checkout.Tests in one folder, with a config naming the report 'Shop' at version 1.0, and a run 'ci'", async (tool, token) =>
            {
                File.WriteAllText(tool.PathOf("Checkout.Tests.csproj"), "<Project />");
                File.WriteAllText(tool.PathOf("Checkout.slnx"), "<Solution><Project Path=\"Checkout.Tests.csproj\" /></Solution>");
                File.WriteAllText(tool.PathOf(AssuranceConfig.FileName), """{ "report": { "title": "Shop", "version": "1.0" } }""");
                await tool.SaveAsync("artifacts/assurance/ci", ToolUnderTest.Evidence("ci", ToolSteps.Day(1), ToolUnderTest.Journey("Pays by card", "checkout")), token);
            })
            .When("'dotnet assurance report Checkout.slnx' runs", (tool, token) => tool.ReportAsync(new() { Target = "Checkout.slnx" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled("Shop 1.0"))
            .And(ReportSteps.TheReportShows("<h2>Checkout.Tests</h2>"))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .VerifyAsync();

    [Fact]
    public Task ProjectsCanUseTheSameContextId() =>
        Scenario.Define<ToolUnderTest>("Projects in a solution can use the same business context ID for different areas", CliBehaviour.SolutionReports,
                description: "Each project's areas are its own, so two teams don't have to agree on context IDs.")
            .Given("All.slnx lists Runtime.Tests, whose context 'reading' is 'Reading at run time', and Core.Tests, whose 'reading' is 'Reading definitions'",
                async (tool, token) =>
                {
                    tool.Solution("Runtime.Tests", "Core.Tests");
                    await tool.SaveAsync(ToolSteps.Runs("Runtime.Tests", "ci"), ToolUnderTest.Evidence("ci", ToolSteps.Day(1),
                        ToolUnderTest.Journey("Reads the variant", "reading", "Reading at run time")), token);
                    await tool.SaveAsync(ToolSteps.Runs("Core.Tests", "ci"), ToolUnderTest.Evidence("ci", ToolSteps.Day(1),
                        ToolUnderTest.Journey("Reads a definition", "reading", "Reading definitions")), token);
                })
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("Reading at run time"))
            .And(ReportSteps.TheReportShows("Reading definitions"))
            .VerifyAsync();

    [Fact]
    public Task ARunIsChosenInEveryProject() =>
        Scenario.Define<ToolUnderTest>("A run chosen by its ID is reported from every project that has it, and the others are left out",
                CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report --run ci-7' runs beside the solution", (tool, token) => tool.ReportAsync(new() { Run = "ci-7" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Project Search.Tests: no run 'ci-7' in"))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportShows("Sends the invoice"))
            .And(ReportSteps.TheReportDoesNotShow("Late billing"))
            .VerifyAsync();

    [Fact]
    public Task ARunNoProjectHasFails() =>
        Scenario.Define<ToolUnderTest>("A run ID that no project has fails the command", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report --run nightly' runs beside the solution", (tool, token) => tool.ReportAsync(new() { Run = "nightly" }, token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[unknown-run]"))
            .VerifyAsync();

    [Fact]
    public Task ProjectsCanBeNamed() =>
        Scenario.Define<ToolUnderTest>("A solution report can be narrowed to the projects named, whatever their case", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report All.slnx --project billing.tests' runs", (tool, token) =>
                tool.ReportAsync(new() { Target = "All.slnx", Projects = ["billing.tests"] }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("Late billing"))
            .And(ReportSteps.TheReportDoesNotShow("Pays by card"))
            .VerifyAsync();

    [Fact]
    public Task ProjectsCanBeLeftOut() =>
        Scenario.Define<ToolUnderTest>("A solution report can leave out the projects named, whatever their case", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report All.slnx --exclude-project billing.tests' runs", (tool, token) =>
                tool.ReportAsync(new() { Target = "All.slnx", ExcludedProjects = ["billing.tests"] }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportDoesNotShow("Late billing"))
            .VerifyAsync();

    [Fact]
    public Task ASolutionReportCanBeTitledAndWrittenElsewhere() =>
        Scenario.Define<ToolUnderTest>("A solution report can be given a title and written to another folder", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report --title \"Release 4.2\" --output elsewhere' runs beside the solution", (tool, token) =>
                tool.ReportAsync(new() { Title = "Release 4.2", Output = "elsewhere" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsWrittenTo("elsewhere/report.html"))
            .And(ReportSteps.TheReportIsTitled("Release 4.2"))
            .VerifyAsync();

    [Fact]
    public Task EvidenceNamedWithInputIsReportedOnItsOwn() =>
        Scenario.Define<ToolUnderTest>("Evidence named with --input is reported on its own, even beside a solution", CliBehaviour.SolutionReports)
            .Given(ToolSteps.ASolutionWithRuns)
            .When("'dotnet assurance report --input <Checkout.Tests' old run> --output old' runs beside the solution", (tool, token) =>
                tool.ReportAsync(new() { Inputs = [ToolSteps.Runs("Checkout.Tests", "old")], Output = "old" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliDoesNotSay("Solution:"))
            .And(ReportSteps.TheReportShows("Old checkout"))
            .And(ReportSteps.TheReportDoesNotShow("Late billing"))
            .VerifyAsync();

    [Fact]
    public Task ASolutionWithoutAConfiguredProjectSaysHowToAddOne() =>
        Scenario.Define<ToolUnderTest>("A solution without a configured project says how to configure one", CliBehaviour.SolutionReports)
            .Given("All.slnx lists only App, which has no config", tool => tool.Solution("App"))
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("dotnet assurance init"))
            .VerifyAsync();

    [Fact]
    public Task AFormatIsGivenOneResultsFolderPerProject() =>
        Scenario.Define<ToolUnderTest>("A format writing a solution report is given each project's run, named as the project's config names it",
                CliBehaviour.SolutionReports)
            .Given("All.slnx lists Checkout.Tests and Billing.Tests titled 'Billing', each with run 'ci-7'", async (tool, token) =>
            {
                tool.Solution("Checkout.Tests", "Billing.Tests");
                File.WriteAllText(tool.PathOf($"tests/Billing.Tests/{AssuranceConfig.FileName}"), """{ "report": { "title": "Billing" } }""");
                await tool.SaveAsync(ToolSteps.Runs("Checkout.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", ToolSteps.Day(1), ToolUnderTest.Journey("Pays by card", "checkout")), token);
                await tool.SaveAsync(ToolSteps.Runs("Billing.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", ToolSteps.Day(1), ToolUnderTest.Journey("Sends the invoice", "billing")), token);
            })
            .And("The report is written through a format that records what it is given", tool => tool.Format = new RecordingFormat())
            .When("'dotnet assurance report --context billing' runs beside the solution", (tool, token) =>
                tool.ReportAsync(new() { Contexts = ["billing"] }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("The format is given each project's run folder, with its one verification, in the solution's order", tool =>
            {
                ReportFormatContext context = Recorded(tool);
                Assert.Equal([tool.PathOf(ToolSteps.Runs("Checkout.Tests", "ci-7")), tool.PathOf(ToolSteps.Runs("Billing.Tests", "ci-7"))],
                    context.Inputs.Select(input => input.Results.Directory));
                Assert.All(context.Inputs, input => Assert.Single(input.Results.Verifications));
            })
            .And("Each project is named as its config names it", tool => Assert.Equal(["Checkout.Tests", "Billing"], Recorded(tool).Inputs.Select(input => input.Project)))
            .And("The format is given the solution's name as the title, the contexts asked for, and the solution's report folder", tool =>
            {
                ReportFormatContext context = Recorded(tool);
                Assert.Equal("All", context.DefaultTitle);
                Assert.Equal(["billing"], context.Contexts);
                Assert.Equal(tool.PathOf("artifacts/assurance/report"), context.OutputDirectory);
            })
            .VerifyAsync();

    [Fact]
    public Task AnAreaAnyProjectLeavesOutIsLeftOutOfTheSolutionReport() =>
        Scenario.Define<ToolUnderTest>("A solution report leaves out the areas any project's config leaves out, and gives the format that list",
                CliBehaviour.SolutionReports)
            .Given("All.slnx lists Checkout.Tests, which ran 'Pays by card' in checkout and 'Retries the gateway' in technical, " +
                "and Billing.Tests, whose config leaves out technical, which ran 'Sends the invoice' in billing", async (tool, token) =>
            {
                tool.Solution("Checkout.Tests", "Billing.Tests");
                File.WriteAllText(tool.PathOf($"tests/Billing.Tests/{AssuranceConfig.FileName}"), """{ "report": { "excludeContexts": [ "technical" ] } }""");
                await tool.SaveAsync(ToolSteps.Runs("Checkout.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", ToolSteps.Day(1),
                    ToolUnderTest.Journey("Pays by card", "checkout"), ToolUnderTest.Journey("Retries the gateway", "technical")), token);
                await tool.SaveAsync(ToolSteps.Runs("Billing.Tests", "ci-7"), ToolUnderTest.Evidence("ci-7", ToolSteps.Day(1), ToolUnderTest.Journey("Sends the invoice", "billing")), token);
            })
            .And("The report is written through a format that records what it is given", tool => tool.Format = new RecordingFormat())
            .When("'dotnet assurance report' runs beside the solution", (tool, token) => tool.ReportAsync(new(), token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Report leaves out business contexts: technical."))
            .And("The format is given technical to leave out", tool => Assert.Equal(["technical"], Recorded(tool).ExcludedContexts))
            .VerifyAsync();

    private static ReportFormatContext Recorded(ToolUnderTest tool) =>
        Assert.IsType<RecordingFormat>(tool.Format).Context ?? throw new global::Xunit.Sdk.XunitException("The format wasn't asked to write anything.");

    private sealed class RecordingFormat : IReportFormat
    {
        public ReportFormatContext? Context { get; private set; }

        public string Name => "recording";

        public Task<ReportFormatResult> WriteAsync(ReportFormatContext context, CancellationToken cancellationToken = default)
        {
            Context = context;
            return Task.FromResult(new ReportFormatResult { Path = Path.Combine(context.OutputDirectory, "report.txt") });
        }
    }
}
