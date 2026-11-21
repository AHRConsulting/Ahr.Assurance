using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class CheckingOutcomes
{
    [Fact]
    public Task APassingAssertionIsAPassingCheckWithNothingToCompare() =>
        Scenario.Define<ScenarioUnderTest>("A passing assertion is a passing check, with nothing to compare", Capabilities.Checks)
            .Given("A scenario whose checks assert synchronously, after awaiting, and with a token", scenario => scenario.Use(
                Scenario.Define("Factories", () => new State())
                    .Given(Steps.Setup).And(Steps.AsyncSetup).When(Steps.Operation)
                    .Then(Steps.SyncAssertion).And(Steps.TaskAssertion).And(Steps.TokenAssertion)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("Each check says its assertion completed, with no expected or actual value", scenario => Assert.All(scenario.Result.Steps.Skip(3), step =>
                Assert.Equal(("Assertion completed.", null, null), (step.Evaluation.Explanation, step.Evaluation.Expected, step.Evaluation.Actual))))
            .VerifyAsync();

    [Fact]
    public Task AFailedAssertionIsAFailedCheck() =>
        Scenario.Define<ScenarioUnderTest>("A failed assertion is a failed check", Capabilities.Checks)
            .Given("A scenario whose check asserts the wrong count", scenario => scenario.Use(
                Scenario.Define("Wrong count", () => new State()).Given(Steps.Setup).When(Steps.Operation).Then(Steps.FailedAssertion)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .And("The check names the assertion library that failed it", scenario =>
                Assert.Contains("xUnit assertion failed:", scenario.Step(3).Evaluation.Explanation))
            .VerifyAsync();

    [Fact]
    public Task AnyOtherExceptionInACheckStopsTheScenario() =>
        Scenario.Define<ScenarioUnderTest>("Any other exception in a check stops the scenario", Capabilities.Checks,
                description: "A check that couldn't run hasn't failed: it is reported as an error in the test, not as the behaviour being wrong.")
            .Given("A scenario whose check throws an IOException", scenario => scenario.Use(
                Scenario.Define("Broken check", () => new State()).Given(Steps.Setup).When(Steps.Operation).Then(Steps.BrokenAssertion)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Broken callback"))
            .And("The error keeps the exception's type", scenario => Assert.Equal(typeof(IOException).FullName, scenario.Error.ExceptionType))
            .VerifyAsync();

    [Fact]
    public Task AnAssertionLibraryIsRecognisedByItsMarker() =>
        Scenario.Define<ScenarioUnderTest>("An assertion library's own exceptions are recognised by xUnit's marker interface", Capabilities.Checks)
            .Given("A scenario whose check throws an exception marked as an assertion", scenario => scenario.Use(
                Scenario.Define("Custom assertion", () => new State()).Given(Steps.Setup).When(Steps.Operation)
                    .Then("Custom assertion", _ => throw new MarkedAssertion())))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .VerifyAsync();

    [Fact]
    public Task AStructuredResultIsKeptWhicheverWayItIsReturned() =>
        Scenario.Define<ScenarioUnderTest>("A structured result is kept whichever way a check returns it", Capabilities.Checks)
            .Given("A scenario whose checks return an inconclusive result, synchronously, after awaiting, with a token and explicitly", scenario => scenario.Use(
                Scenario.Define("Structured", () => new State()).Given(Steps.Setup).When(Steps.Operation)
                    .Then(Steps.Structured).And(Steps.StructuredTask).And(Steps.StructuredToken).And(Steps.ExplicitExpectation)))
            .When(RunSteps.ItIsRun)
            .Then("Every check keeps its verdict and what it found", scenario => Assert.All(scenario.Result.Steps.Skip(2), step =>
                Assert.Equal((Inconclusive, "Recorded"), (step.Evaluation.Status, step.Evaluation.Actual))))
            .VerifyAsync();

    private sealed class MarkedAssertion() : Exception("Custom assertion failed"), global::Xunit.Sdk.IAssertionException;

    private sealed class State
    {
        public int Value { get; set; }
    }

    private sealed class Steps : ScenarioSteps<State>
    {
        public static ScenarioAction<State> Setup { get; } = CreateActionStep("Ready", state => state.Value = 0);
        public static ScenarioAction<State> AsyncSetup { get; } = CreateActionStep("Async setup", async state =>
        {
            await Task.Yield();
            state.Value++;
        });
        public static ScenarioAction<State> Operation { get; } = CreateActionStep("Act", (state, token) =>
        {
            token.ThrowIfCancellationRequested();
            state.Value++;
            return Task.CompletedTask;
        });
        public static ScenarioExpectation<State> SyncAssertion { get; } = CreateAssertionStep("Check", state => Assert.Equal(2, state.Value));
        public static ScenarioExpectation<State> TaskAssertion { get; } = CreateAssertionStep("Awaited check", async state =>
        {
            await Task.Yield();
            Assert.Equal(2, state.Value);
        });
        public static ScenarioExpectation<State> TokenAssertion { get; } = CreateAssertionStep("Token check", async (state, token) =>
        {
            await Task.Delay(1, token);
            Assert.Equal(2, state.Value);
        });
        public static ScenarioExpectation<State> FailedAssertion { get; } = CreateAssertionStep("Wrong count", state => Assert.Equal(99, state.Value));
        public static ScenarioExpectation<State> BrokenAssertion { get; } = CreateAssertionStep("Broken", (Action<State>)(_ => throw new IOException("Broken callback")));
        public static ScenarioExpectation<State> Structured { get; } = CreateAssertionStep("Evidence", _ => Evidence());
        public static ScenarioExpectation<State> StructuredTask { get; } = CreateAssertionStep("Task evidence", async _ =>
        {
            await Task.Yield();
            return Evidence();
        });
        public static ScenarioExpectation<State> StructuredToken { get; } = CreateAssertionStep("Token evidence", (_, _) => Task.FromResult(Evidence()));
        public static ScenarioExpectation<State> ExplicitExpectation { get; } = CreateExpectationStep("Explicit", _ => Evidence());
        private static ExpectationResult Evidence() => new(Inconclusive, "Unknown", "Expected", "Recorded");
    }
}
