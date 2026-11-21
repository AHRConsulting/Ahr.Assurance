using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class RejectingMistakes
{
    [Fact]
    public Task AScenarioWithoutStepsIsRejected() =>
        Scenario.Define<ScenarioUnderTest>("A scenario without steps is rejected before its context is created", ExecutionCapabilities.Mistakes)
            .Given("A scenario with no steps", scenario => scenario.Use(Scenario.Define("Nothing to run", () => new TrackedCounter(scenario.Calls))))
            .When(RunSteps.ItIsRun)
            .Then(ExecutionSteps.ItIsRejectedBeforeItStarts("requires a When step"))
            .VerifyAsync();

    [Fact]
    public Task StepsOutOfOrderAreRejected() =>
        Scenario.Define<ScenarioUnderTest>("Steps out of order are rejected before any of them runs", ExecutionCapabilities.Mistakes)
            .Given("A scenario with its When before its Given", scenario => scenario.Use(Scenario.Define("When before Given", () => new TrackedCounter(scenario.Calls))
                .When(TrackedCounterSteps.Increment)
                .Given(TrackedCounterSteps.Ready)
                .Then(TrackedCounterSteps.IncrementedOnce)))
            .When(RunSteps.ItIsRun)
            .Then(ExecutionSteps.ItIsRejectedBeforeItStarts("must order steps as Given, When, then Then"))
            .VerifyAsync();

    [Fact]
    public Task AGivenAfterAThenIsRejected() =>
        Scenario.Define<ScenarioUnderTest>("A Given after a Then is rejected; setup belongs at the start", ExecutionCapabilities.Mistakes)
            .Given("A scenario that sets up again after its check", scenario => scenario.Use(Scenario.Define("Given after Then", () => new TrackedCounter(scenario.Calls))
                .Given(TrackedCounterSteps.Ready)
                .When(TrackedCounterSteps.Increment)
                .Then(TrackedCounterSteps.IncrementedOnce)
                .Given(TrackedCounterSteps.Ready)
                .When(TrackedCounterSteps.Increment)
                .Then(TrackedCounterSteps.IncrementedOnce)))
            .When(RunSteps.ItIsRun)
            .Then(ExecutionSteps.ItIsRejectedBeforeItStarts("only a When may follow a Then"))
            .VerifyAsync();

    [Fact]
    public Task AScenarioThatStartsWithAThenIsRejected() =>
        Scenario.Define<ScenarioUnderTest>("A scenario that starts with a check is rejected; a check follows what it checks", ExecutionCapabilities.Mistakes)
            .Given("A scenario whose first step is a Then", scenario => scenario.Use(Scenario.Define("Starts with Then", () => new TrackedCounter(scenario.Calls))
                .Then(TrackedCounterSteps.IncrementedOnce)
                .When(TrackedCounterSteps.Increment)
                .Then(TrackedCounterSteps.IncrementedOnce)))
            .When(RunSteps.ItIsRun)
            .Then(ExecutionSteps.ItIsRejectedBeforeItStarts("must start with a Given or When step"))
            .VerifyAsync();

    [Fact]
    public Task AnActionWithoutACheckAfterItIsRejected() =>
        Scenario.Define<ScenarioUnderTest>("A scenario that ends with an action nothing checks is rejected", ExecutionCapabilities.Mistakes)
            .Given("A scenario whose last step is a When", scenario => scenario.Use(Scenario.Define("Ends with When", () => new TrackedCounter(scenario.Calls))
                .Given(TrackedCounterSteps.Ready)
                .When(TrackedCounterSteps.Increment)
                .Then(TrackedCounterSteps.IncrementedOnce)
                .When(TrackedCounterSteps.Increment)))
            .When(RunSteps.ItIsRun)
            .Then(ExecutionSteps.ItIsRejectedBeforeItStarts("must end with a Then step, or a throws step"))
            .VerifyAsync();

    [Fact]
    public Task ScenariosWithTheSameIdCannotBeVerifiedTogether() =>
        Scenario.Define<ScenarioUnderTest>("Scenarios with the same ID can't be verified together", ExecutionCapabilities.Mistakes,
                description: "The report finds a scenario by its ID, so two with one ID would be reported as one.")
            .Given("A scenario with the ID 'renewal'", _ => { })
            .WhenThrows<global::Xunit.Sdk.XunitException>("It is verified twice in one verification", scenario =>
            {
                Scenario<TrackedCounter> renewal = Scenario.Define("A renewal", () => new TrackedCounter(scenario.Calls), id: "renewal")
                    .Given(TrackedCounterSteps.Ready).When(TrackedCounterSteps.Increment).Then(TrackedCounterSteps.IncrementedOnce);
                return AssuranceAssert.VerifyAsync("Same ID twice", [renewal, renewal], ExecutionSteps.Options(scenario));
            })
            .Then(ExecutionSteps.TheVerificationFails("Duplicate scenario id 'renewal'"))
            .And("No step ran", scenario => Assert.Empty(scenario.Calls))
            .VerifyAsync();

    [Fact]
    public Task AVerificationNeedsAScenario() =>
        Scenario.Define<ScenarioUnderTest>("A verification needs at least one scenario", ExecutionCapabilities.Mistakes)
            .Given("No scenarios", _ => { })
            .WhenThrows<global::Xunit.Sdk.XunitException>("They are verified", scenario =>
                AssuranceAssert.VerifyAsync<TrackedCounter>("Nothing", [], ExecutionSteps.Options(scenario)))
            .Then(ExecutionSteps.TheVerificationFails("requires at least one scenario"))
            .VerifyAsync();

    [Theory]
    [InlineData("as the first step")]
    [InlineData("as a check with no Then before it")]
    [InlineData("as an action after a Then")]
    public Task AnAndStepMustFollowAStepOfItsKind(string placement) =>
        Scenario.Define("An And step must follow a step of its own kind", ExecutionCapabilities.Mistakes)
            .Given($"A typed scenario that adds an And step {placement}", () => { })
            .WhenThrows<InvalidOperationException>("The And step is added", () => AddAnd(placement))
            .Then("The mistake is explained", (InvalidOperationException mistake) => Assert.Contains("must follow", mistake.Message))
            .VerifyAsync();

    private static void AddAnd(string placement)
    {
        var scenario = Scenario.Define("And in the wrong place", () => new object());
        var action = ScenarioAction<object>.Synchronous("An action", _ => { });
        var check = ScenarioExpectation<object>.Assertion("A check", _ => { });
        _ = placement switch
        {
            "as the first step" => scenario.And(action),
            "as a check with no Then before it" => scenario.Given(action).And(check),
            _ => scenario.Given(action).When(action).Then(check).And(action),
        };
    }
}
