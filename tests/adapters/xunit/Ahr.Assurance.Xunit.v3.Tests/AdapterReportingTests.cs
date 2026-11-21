using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class AdapterReportingTests
{
    [Theory]
    [InlineData(Satisfied)]
    [InlineData(Unsatisfied)]
    [InlineData(Inconclusive)]
    [InlineData(NotEvaluated)]
    public Task TheReportIsWrittenWhateverTheVerdict(AssuranceStatus status) =>
        Scenario.Define<VerificationUnderTest>("The report is written before the verdict is given to the test, whatever the verdict", XunitBehaviour.Reports)
            .Given(ARenewalWhoseCheckIs(status))
            .And(AReportFile)
            .When(ItIsVerified)
            .Then("The test passes only when the check is satisfied, and otherwise names the verdict", verification =>
            {
                if (status == Satisfied)
                {
                    Assert.Null(verification.Failure);
                }
                else
                {
                    Assert.Contains(status.ToString(), verification.TestFailure.Message);
                }
            })
            .And(TheReportSays("Customer retains access"))
            .And("The report shows what was expected only when the check didn't pass", verification =>
                Assert.Equal(status != Satisfied, verification.Report.Contains("Expected:", StringComparison.Ordinal)))
            .VerifyAsync();

    [Fact]
    public Task AReportThatCannotBeWrittenDoesNotHideAFailedCheck() =>
        Scenario.Define<VerificationUnderTest>("A report that can't be written doesn't hide a failed check", AdapterBehaviour.Delivery)
            .Given(ARenewalWhoseCheckIs(Unsatisfied))
            .And(AFolderIsInTheWayOfTheReport)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Unsatisfied"))
            .And(TheTestFailsSaying("Evidence sink 'HTML' failed"))
            .And(NoReportIsLeftBehind)
            .VerifyAsync();

    [Fact]
    public Task AReportThatCannotBeWrittenFailsAPassingTest() =>
        Scenario.Define<VerificationUnderTest>("A report that can't be written fails a passing test", AdapterBehaviour.Delivery)
            .Given(ARenewalWhoseCheckIs(Satisfied))
            .And(AFolderIsInTheWayOfTheReport)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Evidence sink 'HTML' failed"))
            .VerifyAsync();

    [Fact]
    public Task AScenarioThatStopsStillGetsAReport() =>
        Scenario.Define<VerificationUnderTest>("A scenario that stops with an error still gets a report of the steps it ran", XunitBehaviour.Reports)
            .Given("A scenario whose action fails because a service is unavailable", verification => verification.Use(
                Scenario.Define<object>("Broken", id: "broken")
                    .Given("Ready", _ => { })
                    .When("Operation", (Action<object>)(_ => throw new IOException("service unavailable")))
                    .Then("Access", _ => new ExpectationResult(Satisfied, "Access"))))
            .And(AReportFile)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("service unavailable"))
            .And(TheReportSays("Execution error"))
            .And(TheReportSays("Not evaluated"))
            .VerifyAsync();

    [Fact]
    public Task AVerificationWithNoScenariosStillGetsAReport() =>
        Scenario.Define<VerificationUnderTest>("A verification with no scenarios still gets a report of the error", XunitBehaviour.Reports)
            .Given(NoScenarios("Invalid definition"))
            .And(AReportFile)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("invalid-definition"))
            .And(TheReportSays("Execution error"))
            .And(TheReportSays("0 journeys"))
            .VerifyAsync();

    [Fact]
    public Task AnErrorAndAReportThatCannotBeWrittenAreBothReported() =>
        Scenario.Define<VerificationUnderTest>("A verification error and a report that can't be written are both reported", AdapterBehaviour.Delivery)
            .Given(NoScenarios("Both errors"))
            .And(AFolderIsInTheWayOfTheReport)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("invalid-definition"))
            .And(TheTestFailsSaying("Evidence sink 'HTML' failed"))
            .VerifyAsync();
}
