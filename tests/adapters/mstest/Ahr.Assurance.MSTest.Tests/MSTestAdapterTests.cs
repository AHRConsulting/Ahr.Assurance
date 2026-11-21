using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Execution;
using static System.FormattableString;
using static Ahr.Assurance.Adapters.Conformance.AdapterSteps;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.MSTest.Tests.MSTestFramework;
using static Ahr.Assurance.MSTest.Tests.MSTestSteps;

namespace Ahr.Assurance.MSTest.Tests;

/// <remarks>Shared with the MSTest 3 tests, so the tests pass their context, which every MSTest version can.</remarks>
[TestClass]
public sealed class MSTestAdapterTests
{
    public static IEnumerable<object[]> Amounts => [["GBP", 100], ["EUR", -1]];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task ChecksWrittenWithMSTestAssertionsPass() =>
        Define(TestContext, "A scenario checked with MSTest's Assert passes when its checks pass", AdapterBehaviour.Verdicts)
            .Given("A scenario that adds up a basket and checks the total with Assert.AreEqual", adapter =>
            {
                var total = 0;
                adapter.Use(Scenario.Define("A basket adds up its lines", () => new object())
                    .Given("A basket with two lines", _ => total = 0)
                    .When("The lines are added", _ => total = 2 + 3)
                    .Then("The total is their sum", _ => Assert.AreEqual(5, total)));
            })
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And(TheVerdictIs(Satisfied))
            .VerifyAsync(TestContext);

    [TestMethod]
    public Task AFailedAssertionFailsTheTestTheMSTestWay() =>
        Define(TestContext, "A failed MSTest assertion fails the test the MSTest way, naming MSTest", AdapterBehaviour.Verdicts)
            .Given("A scenario whose check expects 1 and finds 2", adapter => adapter.Use(adapter.Simple(_ => Assert.AreEqual(1, Total(2)), "A failing check")))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("MSTest assertion failed"))
            .And(TheVerdictIs(Unsatisfied))
            .VerifyAsync(TestContext);

    [TestMethod]
    public Task EvidenceNamesTheTestWhoseContextIsPassed() =>
        Define(TestContext, "Evidence names the test whose TestContext a scenario is verified with", AdapterBehaviour.Identity)
            .Given("A passing scenario", _ => { })
            .When("The scenario is verified with the test's TestContext", async adapter => _ = await Scenario.Define("Verified with a context", () => new object())
                .Given("Ready", _ => { })
                .When("Act", _ => { })
                .Then("Nothing fails", _ => { })
                .VerifyAsync(TestContext, adapter.Options))
            .Then(TheEvidenceNamesTheTest(nameof(EvidenceNamesTheTestWhoseContextIsPassed)))
            .And("The evidence's producer is the test assembly", adapter =>
                Assert.AreEqual(typeof(MSTestAdapterTests).Assembly.GetName().Name, adapter.Evidence.Producer))
            .And("The scenario isn't recorded as an example", adapter => Assert.IsTrue(adapter.Evidence.Report.Run.Scenarios.Single().Example.IsEmpty))
            .VerifyAsync(TestContext);

    [TestMethod]
    public Task ScenariosVerifiedTogetherNameTheTestWhoseContextIsPassed() =>
        Define(TestContext, "Scenarios verified together with a TestContext name its test", AdapterBehaviour.Identity)
            .Given("A passing scenario", _ => { })
            .When("It is verified through AssuranceAssert with the test's TestContext", async adapter => _ = await AssuranceAssert.VerifyAsync("Verified together",
                [Scenario.Define("Verified together with a context", () => new object()).Given("Ready", _ => { }).When("Act", _ => { }).Then("Nothing fails", _ => { })],
                TestContext, adapter.Options))
            .Then(TheEvidenceNamesTheTest(nameof(ScenariosVerifiedTogetherNameTheTestWhoseContextIsPassed)))
            .VerifyAsync(TestContext);

#if !MSTEST3
    [TestMethod]
    public Task WithoutAContextTheCurrentTestIsNamed() =>
        Define(TestContext, "Without a TestContext, evidence names the current test", AdapterBehaviour.Identity)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Verified without a context")))
            .When(ItIsVerified)
            .Then(TheEvidenceNamesTheTest(nameof(WithoutAContextTheCurrentTestIsNamed)))
            .VerifyAsync(TestContext);
#endif

    [TestMethod]
    [DataRow("GBP", 100)]
    [DataRow("EUR", -1)]
    public Task DataRowsAreExamples(string currency, int pence) =>
        TheRowIsAnExample("Each DataRow is recorded as an example of its scenario", currency, pence);

    [TestMethod]
    [DynamicData(nameof(Amounts))]
    public Task DynamicDataRowsAreExamples(string currency, int pence) =>
        TheRowIsAnExample("Each DynamicData row is recorded as an example of its scenario", currency, pence);

    private Task TheRowIsAnExample(string name, string currency, int pence) =>
        Define(TestContext, name, AdapterBehaviour.Identity)
            .Given("A scenario in a test that has a row for each charge", adapter =>
                adapter.Use(adapter.Simple(_ => Assert.IsFalse(string.IsNullOrEmpty(currency)), "A charge is recorded")))
            .When(ItIsVerified)
            .Then(TheRowIsRecorded("A charge is recorded", Invariant($"currency: {currency}, pence: {pence}")))
            .VerifyAsync(TestContext);

    private static int Total(int value) => value;
}
