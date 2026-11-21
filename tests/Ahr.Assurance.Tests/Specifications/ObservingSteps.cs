using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class ObservingSteps
{
    [Fact]
    public Task AnObserverSeesEachStepAndAttachesToIt() =>
        Scenario.Define<ScenarioUnderTest>("Code that runs after each step sees how it ended, and what it attaches belongs to that step", ExecutionCapabilities.Observing)
            .Given("A scenario with a failing check and a later passing check", scenario => scenario.Use(Scenario.Define("An observed journey")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("A failing check", () => ExpectationResult.Check(false, "Broken", "1", "2"))
                .And("A later check", () => new ExpectationResult(Satisfied, "Fine"))))
            .When(ObserverSteps.ItIsVerifiedWhileObserved)
            .Then(RunSteps.TheStepsRanInTheOrder("start An observed journey, Ready Satisfied, Act Satisfied, A failing check Unsatisfied failed, " +
                "A later check Satisfied, finish An observed journey Unsatisfied failed"))
            .And("Each step's attachment is on that step, and the last one is on the scenario", scenario =>
                Assert.Equal([0, 1, 2, 3, null], scenario.Evidence.Attachments.Select(attachment => attachment.StepIndex)))
            .And("The observer stops observing when its scope ends", _ => Assert.Empty(StepObservers.Current))
            .VerifyAsync();

    [Fact]
    public Task AStepThatStopsTheScenarioIsSeenAsFailed() =>
        Scenario.Define<ScenarioUnderTest>("A step that stops the scenario is seen as failed", ExecutionCapabilities.Observing,
                description: "Capture after a failure, such as a screenshot, must also run when a step throws.")
            .Given("A scenario whose action throws", scenario => scenario.Use(Scenario.Define("A broken journey")
                .Given("Ready", () => { })
                .When("It breaks", () => throw new IOException("unavailable"))
                .Then("Never runs", () => new ExpectationResult(Satisfied, "Fine"))))
            .When(ObserverSteps.ItIsVerifiedWhileObserved)
            .Then(RunSteps.TheStepsRanInTheOrder("start A broken journey, Ready Satisfied, It breaks NotEvaluated failed, finish A broken journey NotEvaluated failed"))
            .VerifyAsync();

    [Fact]
    public Task AFailingObserverDoesNotFailTheScenario() =>
        Scenario.Define<ScenarioUnderTest>("An observer that fails doesn't fail the scenario", ExecutionCapabilities.Observing)
            .Given("A scenario that passes", scenario => scenario.Use(Scenario.Define("A journey watched by a failing observer")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("Checked", () => new ExpectationResult(Satisfied, "Fine"))))
            .When("The scenario is verified while an observer that always throws watches", async scenario =>
            {
                using (StepObservers.Add(new Throwing()))
                {
                    _ = await scenario.VerifyAsync();
                }
            })
            .Then("The verification completes, and the scenario is satisfied", scenario =>
                Assert.Equal((VerificationExecutionState.Completed, Satisfied), (scenario.Evidence.ExecutionState, scenario.Evidence.Report.Run.Status)))
            .And("Each of the observer's failures is attached where it happened: on the scenario at its start and end, and on each step after it", scenario =>
                Assert.Equal([null, 0, 1, 2, null], scenario.Evidence.Attachments.Where(attachment => attachment.Name == "Throwing failed.txt")
                    .Select(attachment => attachment.StepIndex)))
            .And("The attachment names the error", async (scenario, token) => Assert.Contains("System.InvalidOperationException: observer",
                await File.ReadAllTextAsync(scenario.PathOf(scenario.Evidence.Attachments.First(attachment => attachment.Name == "Throwing failed.txt")), token)))
            .VerifyAsync();

    private sealed class ObserverSteps : ScenarioSteps<ScenarioUnderTest>
    {
        public static ScenarioAction<ScenarioUnderTest> ItIsVerifiedWhileObserved { get; } =
            CreateThrowsStep<global::Xunit.Sdk.XunitException>("The scenario is verified, and fails, while an observer records each step", async scenario =>
            {
                using (StepObservers.Add(new Recorder(scenario.Calls)))
                {
                    _ = await scenario.VerifyAsync();
                }
            });
    }

    private sealed class Recorder(List<string> calls) : IStepObserver
    {
        public Task ScenarioStartingAsync(string scenario, CancellationToken cancellationToken)
        {
            calls.Add("start " + scenario);
            return Task.CompletedTask;
        }

        public Task StepFinishedAsync(StepOutcome step, CancellationToken cancellationToken)
        {
            calls.Add($"{step.Step} {step.Status}{(step.Failed ? " failed" : "")}");
            Attach.Text($"{step.Step}.txt", step.Step);
            return Task.CompletedTask;
        }

        public Task ScenarioFinishedAsync(ScenarioOutcome scenario, CancellationToken cancellationToken)
        {
            calls.Add($"finish {scenario.Scenario} {scenario.Status}{(scenario.Failed ? " failed" : "")}");
            Attach.Text("scenario.txt", scenario.Scenario);
            return Task.CompletedTask;
        }
    }

    private sealed class Throwing : IStepObserver
    {
        public Task ScenarioStartingAsync(string scenario, CancellationToken cancellationToken) => throw new InvalidOperationException("observer");
        public Task StepFinishedAsync(StepOutcome step, CancellationToken cancellationToken) => throw new InvalidOperationException("observer");
        public Task ScenarioFinishedAsync(ScenarioOutcome scenario, CancellationToken cancellationToken) => throw new InvalidOperationException("observer");
    }
}
