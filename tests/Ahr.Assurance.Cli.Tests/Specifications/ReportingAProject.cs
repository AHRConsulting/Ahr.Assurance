using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

public sealed class ReportingAProject
{
    [Fact]
    public Task TheReportIsOfTheNewestRun() =>
        Scenario.Define<ToolUnderTest>("The report is of the newest run, and is written inside that run's folder", CliBehaviour.ProjectReports,
                description: "Run folders are named by run ID, so the newest is chosen by when it ran, not by its name.")
            .Given(ToolSteps.AProjectWithTwoRuns)
            .And("The runs folder also holds a folder that isn't a run", tool =>
                Directory.CreateDirectory(tool.PathOf("Checkout.Tests/artifacts/assurance/not-a-run")))
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("The CLI names the config it read", tool =>
                Assert.Contains($"Config: {tool.PathOf($"Checkout.Tests/{AssuranceConfig.FileName}")}", tool.Said))
            .And(ReportSteps.TheCliSays("Selected run: aa-newer"))
            .And(ReportSteps.TheCliSays("newest of 2 runs; pass --run to choose another"))
            .And(ReportSteps.TheReportIsWrittenTo("Checkout.Tests/artifacts/assurance/aa-newer/report/report.html"))
            .And(ReportSteps.TheReportIsTitled("Release 0.1"))
            .And(ReportSteps.TheReportShows("Newer journey"))
            .VerifyAsync();

    [Theory]
    [InlineData("""{ "report": { "title": "Release", "version": "0.1" } }""", "Release 0.1")]
    [InlineData("""{ "report": { "version": "2.0" } }""", "Checkout.Tests 2.0")]
    public Task TheReportCarriesTheVersion(string config, string title) =>
        Scenario.Define<ToolUnderTest>("A project's report carries the version its config gives, after its title or else the project's name",
                CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .And($"Its config holds {config}", tool => File.WriteAllText(tool.PathOf($"Checkout.Tests/{AssuranceConfig.FileName}"), config))
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled(title))
            .VerifyAsync();

    [Fact]
    public Task ARunCanBeChosenByItsId() =>
        Scenario.Define<ToolUnderTest>("Any run can be chosen by its ID", CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .When("'dotnet assurance report --run zz-older' runs in Checkout.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Checkout.Tests", Run = "zz-older" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsWrittenTo("Checkout.Tests/artifacts/assurance/zz-older/report/report.html"))
            .And(ReportSteps.TheReportShows("Older journey"))
            .VerifyAsync();

    [Fact]
    public Task AnUnknownRunFailsAndListsTheRuns() =>
        Scenario.Define<ToolUnderTest>("A run ID that isn't there fails the command, which lists the runs there are", CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .When("'dotnet assurance report --run missing' runs in Checkout.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Checkout.Tests", Run = "missing" }, token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("No run 'missing'"))
            .And(ReportSteps.TheCliSays("Available: aa-newer, zz-older."))
            .VerifyAsync();

    [Fact]
    public Task AProjectWithoutRunsSaysToRunTheTestsFirst() =>
        Scenario.Define<ToolUnderTest>("A project that hasn't run its tests is told to run them first", CliBehaviour.ProjectReports)
            .Given("Checkout.Tests has a config and no runs", tool => tool.Project("Checkout.Tests"))
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[no-evidence]"))
            .And(ReportSteps.TheCliSays("Run the tests first."))
            .VerifyAsync();

    [Fact]
    public Task WithoutAConfigTheCliSaysHowToStart() =>
        Scenario.Define<ToolUnderTest>("Without a config or evidence to read, the CLI says how to start", CliBehaviour.ProjectReports)
            .Given("A folder with no config in it or above it", _ => { })
            .When(ToolSteps.TheReportRunsIn("."))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[no-input]"))
            .And(ReportSteps.TheCliSays("dotnet assurance init"))
            .VerifyAsync();

    [Fact]
    public Task EvidenceNamedWithInputIsReportedWhereAndAsAsked() =>
        Scenario.Define<ToolUnderTest>("A run folder named with --input is reported to the folder and with the title given", CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .When("'dotnet assurance report --input <zz-older> --output other --title Hotfix' runs in Checkout.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Checkout.Tests", Inputs = ["Checkout.Tests/artifacts/assurance/zz-older"], Output = "other", Title = "Hotfix" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsWrittenTo("other/report.html"))
            .And(ReportSteps.TheReportIsTitled("Hotfix"))
            .And(ReportSteps.TheReportShows("Older journey"))
            .VerifyAsync();

    [Fact]
    public Task AReportIsNotWrittenOverOtherFiles() =>
        Scenario.Define<ToolUnderTest>("A report isn't written into a folder that already holds files", CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .And("A report of 'zz-older' was written to 'other'", async (tool, token) =>
            {
                await tool.ReportAsync(new() { Folder = "Checkout.Tests", Inputs = ["Checkout.Tests/artifacts/assurance/zz-older"], Output = "other" }, token);
                tool.Forget();
            })
            .When("'dotnet assurance report --input <zz-older> --output other' runs again", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Checkout.Tests", Inputs = ["Checkout.Tests/artifacts/assurance/zz-older"], Output = "other" }, token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[output-not-empty]"))
            .VerifyAsync();

    [Theory]
    [InlineData("tests/Checkout.Tests", null)]
    [InlineData("tests/Checkout.Tests/Pages", null)]
    [InlineData(".", "tests/Checkout.Tests/Checkout.Tests.csproj")]
    public Task TheProjectIsTheOneTheToolRunsIn(string folder, string? target) =>
        Scenario.Define<ToolUnderTest>("The CLI reports the project whose folder it runs in, or the project it is given", CliBehaviour.ProjectReports,
                description: "A team runs the command where it works, as it runs 'dotnet test', even inside a solution.")
            .Given(ToolSteps.ASolutionWithRuns)
            .And("Checkout.Tests has a Pages folder", tool => Directory.CreateDirectory(tool.PathOf("tests/Checkout.Tests/Pages")))
            .When("'dotnet assurance report' runs in the folder, given the project if there is one", (tool, token) =>
                tool.ReportAsync(new() { Folder = folder, Target = target }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliDoesNotSay("Solution:"))
            .And(ReportSteps.TheReportIsWrittenTo("tests/Checkout.Tests/artifacts/assurance/ci-7/report/report.html"))
            .VerifyAsync();

    [Fact]
    public Task AProjectReportIsTitledAfterItsProject() =>
        Scenario.Define<ToolUnderTest>("A project's report is titled after the project, says when it was tested, and shows every area", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When(ToolSteps.TheReportRunsIn("Payments.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportIsTitled("Payments.Tests"))
            .And(ReportSteps.TheReportShows("Tested 1 January 1970, 00:00 UTC. Report generated "))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportShows("Finds a product"))
            .And(ReportSteps.TheReportDoesNotShow("Other areas are not shown"))
            .And(ReportSteps.TheReportDoesNotShow("id=\"projects\""))
            .VerifyAsync();

    [Fact]
    public Task AContextDefinedTwoWaysInAProjectIsRefused() =>
        Scenario.Define<ToolUnderTest>("A business context ID defined two ways in one project fails the command, says which, and writes nothing",
                CliBehaviour.ProjectReports)
            .Given("Payments.Tests ran two tests whose context 'checkout' is 'Checkout' in one and 'Paying' in the other", async (tool, token) =>
            {
                tool.Project("Payments.Tests");
                await tool.SaveAsync("Payments.Tests/artifacts/assurance/run", ToolUnderTest.Evidence("run", ToolSteps.Day(1),
                    ToolUnderTest.Journey("Pays by card", "checkout", "Checkout")), token);
                await tool.SaveAsync("Payments.Tests/artifacts/assurance/run", ToolUnderTest.Evidence("run", ToolSteps.Day(1),
                    ToolUnderTest.Journey("Pays by voucher", "checkout", "Paying")), token, file: "b");
            })
            .When(ToolSteps.TheReportRunsIn("Payments.Tests"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[context-conflict]"))
            .And(ReportSteps.TheCliSays("'checkout': 'Checkout' and 'Paying'"))
            .And("No report folder is written", tool => Assert.False(Directory.Exists(tool.PathOf("Payments.Tests/artifacts/assurance/run/report"))))
            .VerifyAsync();

    [Fact]
    public Task AContextReportShowsOnlyThatArea() =>
        Scenario.Define<ToolUnderTest>("A report of one business context shows only that area, and says the others are left out", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When("'dotnet assurance report --context checkout' runs in Payments.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Payments.Tests", Contexts = ["checkout"] }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Report shows business contexts: checkout."))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportDoesNotShow("Finds a product"))
            .And(ReportSteps.TheReportShows("Other areas are not shown"))
            .VerifyAsync();

    [Fact]
    public Task ALeftOutAreaIsNotShown() =>
        Scenario.Define<ToolUnderTest>("A report can leave out a business context, and says which area it leaves out", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When(ToolSteps.TheReportLeavesOut("search"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Report leaves out business contexts: search."))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportDoesNotShow("Finds a product"))
            .And(ReportSteps.TheReportShows("except search, which this report leaves out"))
            .VerifyAsync();

    [Fact]
    public Task TheConfigCanLeaveOutAnArea() =>
        Scenario.Define<ToolUnderTest>("report.excludeContexts in the config leaves an area out of every report of the project", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsLeavingOutSearch)
            .When(ToolSteps.TheReportRunsIn("Payments.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Report leaves out business contexts: search."))
            .And(ReportSteps.TheReportShows("Pays by card"))
            .And(ReportSteps.TheReportDoesNotShow("Finds a product"))
            .VerifyAsync();

    [Fact]
    public Task NamingAnAreaShowsItEvenWhenTheConfigLeavesItOut() =>
        Scenario.Define<ToolUnderTest>("--context shows an area the config leaves out, since the command names what it reports",
                CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsLeavingOutSearch)
            .When("'dotnet assurance report --context search' runs in Payments.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Payments.Tests", Contexts = ["search"] }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("Finds a product"))
            .And(ReportSteps.TheCliDoesNotSay("leaves out"))
            .VerifyAsync();

    [Fact]
    public Task ExcludeContextReplacesTheConfigsList() =>
        Scenario.Define<ToolUnderTest>("--exclude-context replaces the areas the config leaves out", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsLeavingOutSearch)
            .When(ToolSteps.TheReportLeavesOut("checkout"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShows("Finds a product"))
            .And(ReportSteps.TheReportDoesNotShow("Pays by card"))
            .VerifyAsync();

    [Fact]
    public Task AnUnknownLeftOutAreaIsRefused() =>
        Scenario.Define<ToolUnderTest>("Leaving out an area the run doesn't have fails the command and lists the areas it has, so a typo leaves nothing in",
                CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When(ToolSteps.TheReportLeavesOut("serach"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[unknown-context]"))
            .And(ReportSteps.TheCliSays("'serach'. Available: checkout, search."))
            .VerifyAsync();

    [Fact]
    public Task AnAreaBothShownAndLeftOutIsRefused() =>
        Scenario.Define<ToolUnderTest>("An area named by both --context and --exclude-context fails the command", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When("'dotnet assurance report --context checkout --exclude-context checkout' runs in Payments.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Payments.Tests", Contexts = ["checkout"], ExcludedContexts = ["checkout"] }, token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[context-conflict]"))
            .And(ReportSteps.TheCliSays("'checkout' is both shown with --context and left out with --exclude-context"))
            .VerifyAsync();

    [Fact]
    public Task LeavingOutEveryAreaIsRefused() =>
        Scenario.Define<ToolUnderTest>("Leaving out every area the run has fails the command instead of writing an empty report", CliBehaviour.ProjectReports)
            .Given(ToolSteps.PaymentsWithTwoAreas)
            .When(ToolSteps.TheReportLeavesOut("checkout", "search"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("[nothing-to-report]"))
            .And("No report folder is written", tool => Assert.False(Directory.Exists(tool.PathOf("Payments.Tests/artifacts/assurance/run/report"))))
            .VerifyAsync();

    [Fact]
    public Task AnUnknownFormatFailsAndNamesTheFormatsTheCliWrites() =>
        Scenario.Define<ToolUnderTest>("An unknown format fails the command, which names the formats the CLI writes", CliBehaviour.ProjectReports)
            .Given(ToolSteps.AProjectWithTwoRuns)
            .When("'dotnet assurance report --format csv' runs in Checkout.Tests", (tool, token) =>
                tool.ReportAsync(new() { Folder = "Checkout.Tests", Formats = ["csv"] }, token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("No format 'csv'. This CLI writes html."))
            .VerifyAsync();
}
