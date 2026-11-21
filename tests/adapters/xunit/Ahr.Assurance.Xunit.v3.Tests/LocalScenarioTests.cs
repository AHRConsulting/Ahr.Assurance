using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Execution;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class LocalScenarioTests
{
    [Fact]
    public Task AnInlineScenarioKeepsItsFailedCheckWhenItsReportFails() =>
        Scenario.Define<VerificationUnderTest>("An inline scenario's failed check isn't hidden by a report that can't be written, and no file is left behind",
                AdapterBehaviour.Delivery)
            .Given("An inline scenario whose check fails", verification => verification.Use(Scenario.Define("Combined local errors")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("Mismatch", () => Assert.Fail("Original local assertion"))))
            .And(AReportWriterThatFails("Local report failure"))
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Unsatisfied"))
            .And(TheTestFailsSaying("Original local assertion"))
            .And(TheTestFailsSaying("Local report failure"))
            .And(NoReportIsLeftBehind)
            .VerifyAsync();
}
