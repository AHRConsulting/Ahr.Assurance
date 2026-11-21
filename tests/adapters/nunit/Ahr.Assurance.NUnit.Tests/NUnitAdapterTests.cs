using System.Globalization;
using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using NUnit.Framework.Internal;
using static Ahr.Assurance.Adapters.Conformance.AdapterSteps;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.NUnit.Tests.NUnitFramework;
using static Ahr.Assurance.NUnit.Tests.NUnitSteps;

namespace Ahr.Assurance.NUnit.Tests;

public sealed class NUnitAdapterTests
{
    [Test]
    public Task ChecksWrittenWithAssertThatPass() =>
        Define("A scenario checked with Assert.That passes when its checks pass", AdapterBehaviour.Verdicts)
            .Given("A scenario that adds up a basket and checks the total with Assert.That", adapter =>
            {
                var total = 0;
                adapter.Use(Scenario.Define("A basket adds up its lines", () => new object())
                    .Given("A basket with two lines", _ => total = 0)
                    .When("The lines are added", _ => total = 2 + 3)
                    .Then("The total is their sum", _ => Assert.That(total, Is.EqualTo(5))));
            })
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And(TheVerdictIs(Satisfied))
            .VerifyAsync();

    [Test]
    public Task AnAndStepKeepsItsPhase() =>
        Define("An And step belongs to the phase before it, and state flows from step to step", NUnitBehaviour.Steps)
            .Given("A scenario with an And after its Given and after its Then, whose steps record that they ran", adapter => adapter.Use(
                Scenario.Define("And follows its phase", () => new List<string>())
                    .Given("A first setup", calls => calls.Add("given"))
                    .And("A second setup", calls => calls.Add("given and"))
                    .When("Act", calls => calls.Add("when"))
                    .Then("It ran in order", calls => Assert.That(string.Join(", ", calls), Is.EqualTo("given, given and, when")))
                    .And("A further check", calls => Assert.That(calls, Has.Count.EqualTo(3)))))
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("Each step is recorded in its phase", adapter =>
                Assert.That(string.Join(", ", adapter.Steps.Select(step => step.Kind)), Is.EqualTo("Given, Given, When, Then, Then")))
            .VerifyAsync();

    [Test]
    public Task AFailureTheTestHandlesStaysHandled()
    {
        var recorded = -1;
        return Define("A failure the test catches doesn't fail the test", NUnitBehaviour.Steps)
            .Given("A scenario whose check fails with Assert.That", adapter => adapter.Use(adapter.Simple(_ => Assert.That(1, Is.EqualTo(2)), "A failing check")))
            .When("The scenario is verified through the adapter, and the test catches the failure", async (adapter, token) =>
            {
                await adapter.VerifyAsync(token);
                recorded = TestExecutionContext.CurrentContext.CurrentResult.AssertionResults.Count;
            })
            .Then(TheTestFailsNatively)
            .And(TheVerdictIs(Unsatisfied))
            .And("No failed assertion is recorded on the test", _ => Assert.That(recorded, Is.Zero))
            .VerifyAsync();
    }

    [Test]
    public Task AssertMultipleIsOneFailedCheck() =>
        Define("Failures collected by Assert.Multiple are one failed check", NUnitBehaviour.Steps)
            .Given("A scenario whose check makes two failing assertions inside Assert.Multiple", adapter => adapter.Use(adapter.Simple(_ => Assert.Multiple(() =>
            {
                Assert.That(1, Is.EqualTo(2));
                Assert.That("a", Is.EqualTo("b"));
            }), "Two checks at once")))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(StepIs(3, Unsatisfied))
            .And(StepSays(3, "NUnit assertion failed"))
            .VerifyAsync();

    [Test]
    public Task OutputWrittenInAStepReachesTheTest() =>
        Define("Output written inside a step reaches the test", NUnitBehaviour.Steps)
            .Given("A scenario whose check writes a note to TestContext.Out", adapter => adapter.Use(
                adapter.Simple(_ => TestContext.Out.WriteLine("note from the step"), "A step that writes output")))
            .When("The scenario is verified through the adapter, and the test's output is read", async (adapter, token) =>
            {
                await adapter.VerifyAsync(token);
                adapter.Output = adapter.Driver.CapturedOutput;
            })
            .Then(TheTestPasses)
            .And("The test's output has the note", adapter => Assert.That(adapter.Output, Does.Contain("note from the step")))
            .VerifyAsync();

    [Test]
    public Task EvidenceNamesTheNUnitTest() =>
        Define("Evidence names the NUnit test by its ID and full name", AdapterBehaviour.Identity)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Identity")))
            .When(ItIsVerified)
            .Then("The evidence has NUnit's ID and full name for the test", adapter =>
            {
                Assert.That(adapter.Evidence.TestId, Is.EqualTo(TestContext.CurrentContext.Test.ID));
                Assert.That(adapter.Evidence.TestName, Is.EqualTo(TestContext.CurrentContext.Test.FullName));
            })
            .And("The evidence's producer is the test assembly", adapter =>
                Assert.That(adapter.Evidence.Producer, Is.EqualTo(typeof(NUnitAdapterTests).Assembly.GetName().Name)))
            .VerifyAsync();

    [TestCase("GBP", 100)]
    [TestCase("EUR", -1)]
    public Task TestCaseRowsAreExamples(string currency, int pence) =>
        Define("Each TestCase row is recorded as an example of its scenario", AdapterBehaviour.Identity)
            .Given("A scenario in a test that has a row for each charge", adapter => adapter.Use(adapter.Simple(_ => Assert.That(currency, Is.Not.Empty), "A charge is recorded")))
            .When(ItIsVerified)
            .Then(TheRowIsRecorded("A charge is recorded", string.Create(CultureInfo.InvariantCulture, $"currency: {currency}, pence: {pence}")))
            .VerifyAsync();

    [Test]
    public Task ValuesRowsAreExamples([Values(true, false)] bool online) =>
        Define("Each combination of Values is recorded as an example of its scenario", AdapterBehaviour.Identity)
            .Given("A scenario in a test that runs online and offline", adapter => adapter.Use(adapter.Simple(_ => { }, "The reader works online and offline")))
            .When(ItIsVerified)
            .Then(TheRowIsRecorded("The reader works online and offline", online ? "online: true" : "online: false"))
            .VerifyAsync();
}
