using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class StoppingScenarios
{
    [Fact]
    public Task AnUnexpectedErrorNamesTheStepThatStoppedTheRun() =>
        Scenario.Define<ScenarioUnderTest>("An unexpected error names the step that stopped the run, and why the steps after it didn't run", ExecutionCapabilities.Stopping)
            .Given("A scenario whose renewal service is unavailable", scenario => scenario.Use(Scenario.Define("The renewal service is down")
                .Given("Ready", () => { })
                .When("The renewal service is called", () => throw new IOException("Unavailable"))
                .Then("Access is kept", () => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Unavailable"))
            .And("The error is an execution error that keeps the exception's type", scenario =>
                Assert.Equal(("execution-error", typeof(IOException).FullName), (scenario.Error.Code, scenario.Error.ExceptionType)))
            .And(ExecutionSteps.StepEndsAs(2, NotEvaluated, "This step stopped execution: Unavailable"))
            .And(ExecutionSteps.StepEndsAs(3, NotEvaluated, "Not run; execution stopped: Unavailable"))
            .VerifyAsync();

    [Fact]
    public Task AStepThatReturnsNoTaskIsNamed() =>
        Scenario.Define<ScenarioUnderTest>("A step that returns no task is named in the error, with the fix", ExecutionCapabilities.Stopping)
            .Given("A scenario whose step assigns null to a task and so returns no task", scenario =>
            {
                Task? pending = Task.CompletedTask;
                scenario.Use(Scenario.Define("Returns no task")
#pragma warning disable AHRA101
                    .Given("The pending work is cleared", () => pending = null!)
#pragma warning restore AHRA101
                    .When("Act", () => { })
                    .Then("Check", () => { }));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Step 'The pending work is cleared' returned no task"))
            .And(RunSteps.TheRunStopsWithAnError("use a block body"))
            .And(ExecutionSteps.StepEndsAs(1, NotEvaluated, "This step stopped execution"))
            .VerifyAsync();

    [Fact]
    public Task AFailingStepFromAStepsClassStopsTheRunAndReleasesTheContext() =>
        Scenario.Define<ScenarioUnderTest>("A failing asynchronous step from a steps class stops the run, and its context is still disposed", ExecutionCapabilities.Stopping)
            .Given("A scenario whose asynchronous setup fails after awaiting", scenario => scenario.Use(
                Scenario.Define("A setup that fails", () => new TrackedCounter(scenario.Calls))
                    .Given(TrackedCounterSteps.BrokenAsyncSetup)
                    .When(TrackedCounterSteps.Increment)
                    .Then(TrackedCounterSteps.IncrementedOnce)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Setup failed"))
            .And(ExecutionSteps.StepEndsAs(3, NotEvaluated, "Not run; execution stopped: Setup failed"))
            .And(RunSteps.TheStepsRanInTheOrder("created, disposed"))
            .VerifyAsync();

    [Theory]
    [InlineData("a context created synchronously, cancelled in a synchronous step")]
    [InlineData("a context created asynchronously and disposed asynchronously, cancelled in an asynchronous step")]
    public Task CancellingARunDisposesItsContext(string form) =>
        Scenario.Define<ScenarioUnderTest>("Cancelling a run still disposes its context", ExecutionCapabilities.Stopping,
                description: "A context can hold a database, a browser or a server; a cancelled test must not leave it running.")
            .Given($"A scenario with {form}", scenario => scenario.Use(Cancelling(form, scenario)))
            .When(ExecutionSteps.ItIsRunAndCancelled)
            .Then(RunSteps.TheStepsRanInTheOrder("created, disposed"))
            .VerifyAsync();

    [Fact]
    public Task ARunThatIsAlreadyCancelledCreatesNoContext() =>
        Scenario.Define<ScenarioUnderTest>("A run that is already cancelled doesn't create its context", ExecutionCapabilities.Stopping)
            .Given("A scenario whose context records its creation", scenario => scenario.Use(
                Scenario.Define("Never started", () => new TrackedCounter(scenario.Calls))
                    .Given(TrackedCounterSteps.Ready)
                    .When(TrackedCounterSteps.Increment)
                    .Then(TrackedCounterSteps.IncrementedOnce)))
            .And("The test is already cancelled", scenario => scenario.Cancellation.Cancel())
            .When(ExecutionSteps.ItIsRunAndCancelled)
            .Then<OperationCanceledException>("No context was created", (scenario, _) => Assert.Empty(scenario.Calls))
            .VerifyAsync();

    [Fact]
    public Task ACancellationInACheckReachesTheTestUnchanged()
    {
        OperationCanceledException stop = new("stop");
        return Scenario.Define<ScenarioUnderTest>("A cancellation thrown in a check reaches the test unchanged", ExecutionCapabilities.Stopping,
                description: "A check is never blamed for a cancellation: the test is stopped, not failed.")
            .Given("A scenario whose check is cancelled", scenario => scenario.Use(Scenario.Define("A cancelled check", () => new object())
                .Given("Ready", _ => { })
                .When("Act", _ => { })
                .Then(ScenarioExpectation<object>.Assertion("Check", _ => throw stop))))
            .WhenThrows<OperationCanceledException>("The scenario is run", (scenario, token) => scenario.RunAsync(token))
            .Then<OperationCanceledException>("It is the cancellation the check threw", (_, cancellation) => Assert.Same(stop, cancellation))
            .VerifyAsync();
    }

    private static Scenario<ISession> Cancelling(string form, ScenarioUnderTest scenario) =>
        form.StartsWith("a context created synchronously", StringComparison.Ordinal)
            ? Scenario.Define<ISession>("Cancelled synchronously", () => new SyncSession(scenario.Calls))
                .Given("The test is cancelled", _ => scenario.Cancellation.Cancel())
                .When("Act", session => session.Calls.Add("when"))
                .Then("Not reached", session => session.Calls.Add("then"))
            : Scenario.Define<ISession>("Cancelled asynchronously", async _ =>
                {
                    await Task.Yield();
                    return new AsyncSession(scenario.Calls);
                })
                .Given("Ready", _ => { })
                .When("The test is cancelled", async (_, token) =>
                {
                    await scenario.Cancellation.CancelAsync();
                    token.ThrowIfCancellationRequested();
                })
                .Then("Not reached", session => session.Calls.Add("then"));

    private interface ISession
    {
        List<string> Calls { get; }
    }

    private sealed class SyncSession : ISession, IDisposable
    {
        public SyncSession(List<string> calls)
        {
            Calls = calls;
            Calls.Add("created");
        }

        public List<string> Calls { get; }

        public void Dispose() => Calls.Add("disposed");
    }

    private sealed class AsyncSession : ISession, IAsyncDisposable
    {
        public AsyncSession(List<string> calls)
        {
            Calls = calls;
            Calls.Add("created");
        }

        public List<string> Calls { get; }

        public ValueTask DisposeAsync()
        {
            Calls.Add("disposed");
            return ValueTask.CompletedTask;
        }
    }
}
