using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Execution;
using static System.FormattableString;
using static Ahr.Assurance.Adapters.Conformance.AdapterSteps;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.TUnit.Tests.TUnitFramework;
using static Ahr.Assurance.TUnit.Tests.TUnitSteps;

namespace Ahr.Assurance.TUnit.Tests;

public sealed class TUnitAdapterTests
{
    public static IEnumerable<(string, int)> Amounts() => [("GBP", 100), ("EUR", -1)];

    [Test]
    public Task AwaitedAssertionsWorkInEveryStep() =>
        Define("Awaited TUnit assertions work in every step", AdapterBehaviour.Verdicts)
            .Given("A scenario that awaits a TUnit assertion in its Given and in its Then", adapter =>
            {
                var total = 0;
                adapter.Use(Scenario.Define("A basket adds up its lines", () => new object())
                    .Given("A basket with two lines", async _ => await Assert.That(total).IsEqualTo(0))
                    .When("The lines are added", _ => total = 2 + 3)
                    .Then("The total is their sum", async _ => await Assert.That(total).IsEqualTo(5)));
            })
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And(TheVerdictIs(Satisfied))
            .VerifyAsync();

    [Test]
    public Task AFailedAssertionFailsTheTestTheTUnitWay() =>
        Define("A failed TUnit assertion fails the test the TUnit way, naming TUnit", AdapterBehaviour.Verdicts)
            .Given("A scenario whose check awaits a TUnit assertion that fails", adapter =>
            {
                var total = 1;
                adapter.Use(Scenario.Define("A failing check", () => new object())
                    .Given("Ready", _ => { })
                    .When("Nothing happens", _ => { })
                    .Then("The value is wrong", async _ => await Assert.That(total).IsEqualTo(2)));
            })
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("TUnit assertion failed"))
            .And(TheVerdictIs(Unsatisfied))
            .VerifyAsync();

    [Test]
    public Task EvidenceNamesTheTUnitTest() =>
        Define("Evidence names the TUnit test by its ID and name", AdapterBehaviour.Identity)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Identity")))
            .When(ItIsVerified)
            .Then("The evidence has TUnit's ID and the test's name", async adapter =>
            {
                await Assert.That(adapter.Evidence.TestId).IsEqualTo(TestContext.Current!.Metadata.TestDetails.TestId);
                await Assert.That(adapter.Evidence.TestName).IsEqualTo($"{typeof(TUnitAdapterTests).FullName}.{nameof(EvidenceNamesTheTUnitTest)}");
            })
            .And("The evidence's producer is the test assembly", async adapter => await Assert.That(adapter.Evidence.Producer).IsEqualTo("Ahr.Assurance.TUnit.Tests"))
            .And("The scenario isn't recorded as an example", async adapter =>
                await Assert.That(adapter.Evidence.Report.Run.Scenarios.Single().Example.IsEmpty).IsTrue())
            .VerifyAsync();

    [Test]
    [Arguments("GBP", 100)]
    [Arguments("EUR", -1)]
    public Task ArgumentsRowsAreExamples(string currency, int pence) =>
        TheRowIsAnExample("Each Arguments row is recorded as an example of its scenario", currency, pence);

    [Test]
    [MethodDataSource(nameof(Amounts))]
    public Task MethodDataSourceRowsAreExamples(string currency, int pence) =>
        TheRowIsAnExample("Each MethodDataSource row is recorded as an example of its scenario", currency, pence);

    private static Task TheRowIsAnExample(string name, string currency, int pence) =>
        Define(name, AdapterBehaviour.Identity)
            .Given("A scenario in a test that has a row for each charge", adapter => adapter.Use(Scenario.Define("A charge is recorded", () => new object())
                .Given("A charge", _ => { })
                .When("It is made", _ => { })
                .Then("It is recorded", async _ => await Assert.That(currency).IsNotEmpty())))
            .When(ItIsVerified)
            .Then(TheRowIsRecorded("A charge is recorded", Invariant($"currency: {currency}, pence: {pence}")))
            .VerifyAsync();
}
