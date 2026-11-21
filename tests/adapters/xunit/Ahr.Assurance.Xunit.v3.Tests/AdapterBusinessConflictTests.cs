using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class AdapterBusinessConflictTests
{
    [Fact]
    public Task ConflictingBusinessDeclarationsFailWithoutHidingAFailedCheck() =>
        Scenario.Define<VerificationUnderTest>("Conflicting business declarations fail the verification without hiding a failed check", AdapterBehaviour.Verdicts)
            .Given("Two scenarios with failed checks that describe the same business context differently", verification =>
                verification.Use("Conflicting declarations", [Offline("a", "First"), Offline("b", "Second")]))
            .And(AReportFile)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Conflicting definition"))
            .And(TheTestFailsSaying("Unsatisfied"))
            .And(NoReportIsLeftBehind)
            .VerifyAsync();

    private static Scenario<object> Offline(string name, string description) =>
        Scenario.Define(name, () => new object(), BusinessContext.Define("offline", "Offline access", description)
                .Capability("read", "Read documents")
                .Outcome("o", "Outcome")
                .Build())
            .Given("Ready", _ => { })
            .When("Act", _ => { })
            .Then("Mismatch", _ => new(AssuranceStatus.Unsatisfied, "Original failed check"));
}
