using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

public sealed class TrustingTheEvidence
{
    [Fact]
    public Task EvidenceFromAnotherVersionIsReportedWithAWarning() =>
        Scenario.Define<ToolUnderTest>("Evidence recorded by another version of Ahr.Assurance is reported with a warning", CliBehaviour.Trust,
                description: "A report built by a newer tool from older evidence may not show everything the reader expects.")
            .Given("Checkout.Tests has a run recorded by Ahr.Assurance 0.0.9", (tool, token) =>
            {
                tool.Project("Checkout.Tests");
                return tool.SaveAsync("Checkout.Tests/artifacts/assurance/older", ToolUnderTest.Evidence("older", ToolSteps.Day(1), ToolUnderTest.Journey("Journey")) with
                {
                    AssuranceVersion = "0.0.9"
                }, token);
            })
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays($"Warning: this evidence was recorded by Ahr.Assurance 0.0.9, and this CLI is {AssuranceVersion.Current}."))
            .VerifyAsync();

    [Fact]
    public Task EvidenceFromThisVersionIsReportedWithoutAWarning() =>
        Scenario.Define<ToolUnderTest>("Evidence recorded by this version of Ahr.Assurance is reported without a warning", CliBehaviour.Trust)
            .Given("Checkout.Tests has a run recorded by this version", (tool, token) =>
            {
                tool.Project("Checkout.Tests");
                return tool.SaveAsync("Checkout.Tests/artifacts/assurance/same", ToolUnderTest.Evidence("same", ToolSteps.Day(1), ToolUnderTest.Journey("Journey")), token);
            })
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliDoesNotSay("Warning:"))
            .VerifyAsync();

    [Fact]
    public Task AScenarioIdSharedByTwoTestsIsReportedWithAWarning() =>
        Scenario.Define<ToolUnderTest>("A scenario ID used by two tests is reported with a warning", CliBehaviour.Trust,
                description: "The report can't tell two tests apart when they share an ID, so a reader could take one's result for the other's.")
            .Given("Two tests in run 'same' each recorded a scenario with the ID 'Journey'", async (tool, token) =>
            {
                await tool.SaveAsync("evidence/same", ToolUnderTest.Evidence("same", ToolSteps.Day(1), ToolUnderTest.Journey("Journey")) with { TestId = "first-test" }, token, "first");
                await tool.SaveAsync("evidence/same", ToolUnderTest.Evidence("same", ToolSteps.Day(1), ToolUnderTest.Journey("Journey")) with { TestId = "second-test" }, token, "second");
            })
            .When("'dotnet assurance report --input evidence/same --output report' runs", (tool, token) =>
                tool.ReportAsync(new() { Inputs = ["evidence/same"], Output = "report" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Warning: 1 scenario ID is used by more than one test, so the report cannot tell them apart: 'Journey'."))
            .VerifyAsync();

    [Fact]
    public Task VerificationsAreReportedInTheOrderTheyRan() =>
        Scenario.Define<ToolUnderTest>("Verifications are reported in the order they ran, not the order of their files", CliBehaviour.Trust)
            .Given("File a.json holds 'Ran second' and file b.json holds 'Ran first', which started a second earlier", async (tool, token) =>
            {
                await tool.SaveAsync("evidence/run", ToolUnderTest.Evidence("run", ToolSteps.Day(1).AddSeconds(2), ToolUnderTest.Journey("Ran second")), token, "a");
                await tool.SaveAsync("evidence/run", ToolUnderTest.Evidence("run", ToolSteps.Day(1).AddSeconds(1), ToolUnderTest.Journey("Ran first")), token, "b");
            })
            .When("'dotnet assurance report --input evidence/run --output report' runs", (tool, token) =>
                tool.ReportAsync(new() { Inputs = ["evidence/run"], Output = "report" }, token))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheReportShowsInOrder("Ran first", "Ran second"))
            .VerifyAsync();

    [Fact]
    public Task ARunCanceledBeforeItStartsIsReported() =>
        Scenario.Define<ToolUnderTest>("A run canceled before any scenario ran is still reported", CliBehaviour.Trust,
                description: "A CI run canceled at once leaves evidence with no scenarios, and its report must still say what happened.")
            .Given("Checkout.Tests has a run canceled before any scenario ran", (tool, token) =>
            {
                tool.Project("Checkout.Tests");
                return tool.SaveAsync("Checkout.Tests/artifacts/assurance/canceled", ToolUnderTest.Evidence("canceled", ToolSteps.Day(1)) with
                {
                    ExecutionState = VerificationExecutionState.Canceled,
                    Report = new(new("Journey", ToolSteps.Day(1), TimeSpan.Zero, []), [new("verification-canceled", "The operation was canceled.", null)])
                }, token);
            })
            .When(ToolSteps.TheReportRunsIn("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(2))
            .And(ReportSteps.TheCliDoesNotSay("Sequence contains no elements"))
            .And(ReportSteps.TheReportShows("0 of 0 journeys passed."))
            .And(ReportSteps.TheReportShows("Some checks could not finish properly"))
            .VerifyAsync();
}
