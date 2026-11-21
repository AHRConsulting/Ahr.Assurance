using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Ahr.Foundation;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class WritingScenarios
{
    [Fact]
    public Task InlineStepsShareLocalVariablesAndKeepStructuredResults() =>
        Scenario.Define<ScenarioUnderTest>("Inline steps share local variables and keep structured results", Capabilities.Authoring)
            .Given("A scenario that counts in a local variable, checks it, and ends with a structured check that fails", scenario =>
            {
                var value = 0;
                scenario.Use(Scenario.Define("Local state")
                    .Given("Start at zero", () => value = 0)
                    .When("Increment", async () =>
                    {
                        await Task.Yield();
                        value++;
                    })
                    .Then("It is one", () => Assert.Equal(1, value))
                    .And("It is still one after awaiting", async () =>
                    {
                        await Task.Yield();
                        Assert.Equal(1, value);
                    })
                    .And("It is two", () => ExpectationResult.Check(false, "Deliberate mismatch", "2", $"{value}"))
                    .And("It is two after awaiting", async () =>
                    {
                        await Task.Yield();
                        return ExpectationResult.Check(false, "Deliberate mismatch", "2", $"{value}");
                    }));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.StepIs(3, Satisfied))
            .And(RunSteps.StepIs(4, Satisfied))
            .And("The structured checks keep what they expected and what they found", scenario =>
            {
                Assert.Equal(("2", "1"), (scenario.Step(5).Evaluation.Expected, scenario.Step(5).Evaluation.Actual));
                Assert.Equal(("2", "1"), (scenario.Step(6).Evaluation.Expected, scenario.Step(6).Evaluation.Actual));
            })
            .And(RunSteps.TheScenarioIs(Unsatisfied))
            .VerifyAsync();

    [Fact]
    public Task AnAndStepBelongsToThePhaseBeforeIt() =>
        Scenario.Define<ScenarioUnderTest>("An And step belongs to the phase before it", Capabilities.Authoring)
            .Given("A scenario with an And after its Given, its When and its Then", scenario => scenario.Use(Scenario.Define("And follows its phase")
                .Given("A first setup", () => scenario.Calls.Add("given"))
                .And("A second setup", () => scenario.Calls.Add("given and"))
                .When("Act", () => scenario.Calls.Add("when"))
                .And("Act again", async () =>
                {
                    await Task.Yield();
                    scenario.Calls.Add("when and");
                })
                .Then("A check", () => { })
                .And("A further check", _ => Task.CompletedTask)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("given, given and, when, when and"))
            .And("Each step is recorded in its phase", scenario => Assert.Equal(
                [StepKind.Given, StepKind.Given, StepKind.When, StepKind.When, StepKind.Then, StepKind.Then],
                scenario.Result.Steps.Select(step => step.Kind)))
            .And("An And step is recorded with the And keyword", scenario =>
                Assert.Equal(StepKeyword.And, scenario.Step(2).Keyword))
            .VerifyAsync();

    [Fact]
    public Task AnAndStepBelongsToThePhaseBeforeItInATypedScenario() =>
        Scenario.Define<ScenarioUnderTest>("An And step belongs to the phase before it in a typed scenario", Capabilities.Authoring)
            .Given("A typed scenario with an And after its Given and after its Then", scenario => scenario.Use(
                Scenario.Define("Typed And follows its phase", () => new List<string>())
                    .Given("A first setup", calls => calls.Add("given"))
                    .And("A second setup", calls => calls.Add("given and"))
                    .When("Act", calls => calls.Add("when"))
                    .Then("It ran in order", calls => Assert.Equal(["given", "given and", "when"], calls))
                    .And("A further check", calls => Assert.Equal(3, calls.Count))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("The second step is a Given and the last is a Then", scenario =>
                Assert.Equal((StepKind.Given, StepKind.Then), (scenario.Step(2).Kind, scenario.Result.Steps[^1].Kind)))
            .VerifyAsync();

    [Fact]
    public Task AScenarioCannotStartWithAnd() =>
        Scenario.Define("A scenario cannot start with an And step", Capabilities.Authoring)
            .Given("A new scenario", () => { })
#pragma warning disable AHRA102
            .WhenThrows<InvalidOperationException>("An And step is added first", () => Scenario.Define("Starts with And").And("Too early", () => { }))
#pragma warning restore AHRA102
            .Then("The mistake is explained", (InvalidOperationException mistake) => Assert.Contains("must follow a Given or When", mistake.Message))
            .VerifyAsync();

    [Fact]
    public Task AScenarioCanStartAtItsWhen() =>
        Scenario.Define<ScenarioUnderTest>("A call with nothing to set up starts at its When", Capabilities.Authoring)
            .Given("A scenario of a When and a Then", scenario => scenario.Use(Scenario.Define("A pure call")
                .When("The total is calculated", () => scenario.Calls.Add("when"))
                .Then("It is checked", () => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("when, then"))
            .VerifyAsync();

    [Fact]
    public Task AnInlineScenarioRunsItsStepsOnlyOnce() =>
        Scenario.Define<ScenarioUnderTest>("An inline scenario runs its steps only once, even through its branches", Capabilities.Authoring,
                description: "Inline steps capture the test's local variables, so running them twice would see the first run's state.")
            .Given("A scenario with two Then branches that has already run one of them", async (scenario, token) =>
            {
                Scenario template = Scenario.Define("Single use")
                    .Given("Ready", () => { })
                    .When("Act", () => scenario.Calls.Add("act"));
                _ = await template.Then("Passed", () => { }).RunAsync(token);
                scenario.Use(template.Then("Another branch", () => { }));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("only execute once"))
            .And(RunSteps.TheStepsRanInTheOrder("act"))
            .VerifyAsync();

    [Fact]
    public Task AnInlineScenarioRunTwiceAtOnceRunsOnlyOnce() =>
        Scenario.Define<ScenarioUnderTest>("An inline scenario run twice at once runs only once", Capabilities.Authoring)
            .Given("A scenario that records its action", scenario => scenario.Use(Scenario.Define("Concurrent guard")
                .Given("Ready", async () => await Task.Yield())
                .When("Act", () => scenario.Calls.Add("act"))
                .Then("Once", () => { })))
            .When("It is run twice at the same time", async (scenario, token) =>
            {
                Result<AssuranceRunResult, AssuranceExecutionError>[] outcomes = await Task.WhenAll(scenario.RunAsync(token), scenario.RunAsync(token));
                scenario.Outcome = Assert.Single(outcomes, outcome => outcome.IsFailure);
                _ = Assert.Single(outcomes, outcome => outcome.IsSuccess);
            })
            .Then(RunSteps.TheRunStopsWithAnError("only execute once"))
            .And(RunSteps.TheStepsRanInTheOrder("act"))
            .VerifyAsync();

    [Fact]
    public Task AnUnexpectedErrorStopsTheScenarioAndKeepsWhatRan() =>
        Scenario.Define<ScenarioUnderTest>("An unexpected error stops the scenario and keeps what ran", Capabilities.Authoring)
            .Given("A scenario whose action fails after awaiting", scenario => scenario.Use(Scenario.Define("Broken")
                .Given("Ready", () => { })
                .When("Throw after awaiting", async () =>
                {
                    await Task.Yield();
                    throw new IOException("Action failed");
                })
                .Then("Not reached", () => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Action failed"))
            .And("The steps that ran are kept, and the check after the error was not evaluated", scenario =>
            {
                Assert.Equal(NotEvaluated, scenario.Recorded.Steps[^1].Evaluation.Status);
                Assert.Empty(scenario.Calls);
            })
            .VerifyAsync();

    [Fact]
    public Task CancellingAScenarioStopsIt() =>
        Scenario.Define<ScenarioUnderTest>("Cancelling a scenario stops it with the cancellation", Capabilities.Authoring)
            .Given("A scenario whose action is cancelled", scenario => scenario.Use(Scenario.Define("Cancellation")
                .Given("Ready", () => { })
                .When("Cancel", _ => throw new OperationCanceledException("Stopped"))
                .Then("Not reached", () => scenario.Calls.Add("then"))))
            .WhenThrows<OperationCanceledException>("The scenario is run", (scenario, token) => scenario.RunAsync(token))
            .Then<OperationCanceledException>("The cancellation reaches the test unchanged", (scenario, cancellation) =>
            {
                Assert.Equal("Stopped", cancellation.Message);
                Assert.Empty(scenario.Calls);
            })
            .VerifyAsync();

    [Fact]
    public Task ACancellationRequestedInsideAStepStopsTheNextOne() =>
        Scenario.Define<ScenarioUnderTest>("A cancellation requested inside a step stops the next one", Capabilities.Authoring)
            .Given("A scenario whose action cancels the test's run", scenario => scenario.Use(Scenario.Define("Cancelled from inside")
                .Given("Ready", () => { })
                .When("Cancel synchronously", scenario.Cancellation.Cancel)
                .Then("Not reached", () =>
                {
                    scenario.Calls.Add("then");
                    return new ExpectationResult(Satisfied, "Observed");
                })))
            .WhenThrows<OperationCanceledException>("The scenario is run with the test's cancellation",
                scenario => scenario.RunAsync(scenario.Cancellation.Token))
            .Then<OperationCanceledException>("The check after it was not evaluated", (scenario, _) =>
                Assert.DoesNotContain("then", scenario.Calls))
            .VerifyAsync();

    [Fact]
    public Task ATypedScenarioCreatesAFreshContextForEachRun() =>
        Scenario.Define<ScenarioUnderTest>("A typed scenario creates a fresh context for each run", Capabilities.Authoring)
            .Given("A typed scenario that adds one value to its context and checks there is one", scenario => scenario.Use(
                Scenario.Define("Inferred context", () => new List<int>())
                    .Given("Empty", Assert.Empty)
                    .When("Add", values => values.Add(1))
                    .Then("One value", values => Assert.Single(values))))
            .When(RunSteps.ItIsRun)
            .And(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AnAsyncContextFactoryGetsTheRunsTokenAndItsContextIsDisposed() =>
        Scenario.Define<ScenarioUnderTest>("An async context is created with the test's token and disposed after each run", Capabilities.Authoring)
            .Given("A typed scenario whose context is created asynchronously and is disposable", scenario => scenario.Use(
                Scenario.Define("Async factory", async token =>
                {
                    await Task.Yield();
                    scenario.Calls.Add(token == TestContext.Current.CancellationToken ? "created with the test's token" : "created with another token");
                    return new DisposableContext(scenario.Calls);
                }).Given("Ready", _ => { }).When("Act", _ => { }).Then("Not yet disposed", context => Assert.False(context.Disposed))))
            .When(RunSteps.ItIsRun)
            .And(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("created with the test's token, disposed, created with the test's token, disposed"))
            .VerifyAsync();

    [Fact]
    public Task StepsGetTheRunsTokenAndEveryCheckRuns() =>
        Scenario.Define<ScenarioUnderTest>("Steps get the test's token, and every check runs even after one fails", Capabilities.Authoring)
            .Given("A scenario whose steps take a token, with a failing check before two more", scenario => scenario.Use(Scenario.Define("Token overloads")
                .Given("Ready", token =>
                {
                    scenario.Calls.Add(token == TestContext.Current.CancellationToken ? "given has the test's token" : "given has another token");
                    return Task.CompletedTask;
                })
                .When("Act", () => { })
                .Then("A failing check", async token =>
                {
                    await Task.Yield();
                    Assert.Fail("Intentional assertion failure");
                })
                .And("An independent check", async token =>
                {
                    await Task.Yield();
                    scenario.Calls.Add("independent check ran");
                })
                .And("A structured check", token => Task.FromResult(
                    ExpectationResult.Check(token == TestContext.Current.CancellationToken, "Token retained", "the test's token", "the test's token")))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("given has the test's token, independent check ran"))
            .And("The failing check keeps its message", scenario =>
                Assert.Contains("Intentional assertion failure", scenario.Step(3).Evaluation.Explanation))
            .And(RunSteps.StepIs(5, Satisfied))
            .And(RunSteps.TheScenarioIs(Unsatisfied))
            .VerifyAsync();

    [Fact]
    public Task AFailingCheckAndAFailingReportAreBothReported() =>
        Scenario.Define<ScenarioUnderTest>("A failing check and a report that can't be written are both reported", Capabilities.Authoring)
            .Given("A scenario with a failing check", scenario => scenario.Use(Scenario.Define("Combined local errors")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Mismatch", () => Assert.Fail("Original local assertion"))))
            .And("A report that fails to write", scenario => scenario.Sinks.Add(new BrokenSink()))
            .When(RunSteps.ItIsVerifiedAndFails)
            .Then<global::Xunit.Sdk.XunitException>("The failure names the verdict, the check and the report error", (_, failure) =>
            {
                Assert.Contains("Unsatisfied", failure.Message);
                Assert.Contains("Original local assertion", failure.Message);
                Assert.Contains("Local report failure", failure.Message);
            })
            .VerifyAsync();

    [Fact]
    public Task AScenarioDefinitionBuildsIndependentScenarios() =>
        Scenario.Define<ScenarioUnderTest>("A scenario definition builds scenarios that run independently", Capabilities.Authoring)
            .Given("A definition of a counter that is incremented once", scenario => scenario.Use(new CounterJourney().Build()))
            .When(RunSteps.ItIsRun)
            .And(RunSteps.ItIsRun)
            .And("A second scenario from the same definition is run", async (scenario, token) =>
            {
                scenario.Use(new CounterJourney().Build());
                scenario.Outcome = await scenario.RunAsync(token);
            })
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task VerifyingAScenarioPublishesItsEvidence() =>
        Scenario.Define<ScenarioUnderTest>("Verifying a scenario publishes its evidence and returns its verdict", Capabilities.Authoring)
            .Given("A scenario from a definition", scenario => scenario.Use(new CounterJourney().Build()))
            .When(RunSteps.ItIsVerified)
            .Then("Its evidence names the scenario and its business context", scenario =>
            {
                Assert.Equal("counter", scenario.Sink.Scenario.Id);
                Assert.Equal("Increment independent counters", scenario.Sink.Scenario.Business?.Description);
            })
            .And("Its evidence records the test it came from", scenario =>
                Assert.Equal(TestContext.Current.Test?.TestDisplayName, scenario.Sink.Single.TestName))
            .VerifyAsync();

    [Fact]
    public Task EachStepStartsOnTheCallersSynchronizationContext() =>
        Scenario.Define<ScenarioUnderTest>("Each step starts on the synchronization context the run started on", Capabilities.Authoring,
                description: "As code after an await in a test body does, so a step that leaves the context doesn't take the next step with it.")
            .Given("A scenario whose When leaves the caller's context, and whose Then notes the context it starts on", scenario =>
                scenario.Use(Scenario.Define("Work leaves the context")
                    .Given("The caller's context", () => { })
                    .When("The work continues off it", async () => await Task.Delay(1).ConfigureAwait(false))
                    .Then("The next step notes where it starts", () => scenario.Calls.Add(SynchronizationContext.Current is MarkedContext ? "on it" : "off it"))))
            .When("The scenario is run on a synchronization context", async (scenario, token) =>
            {
                SynchronizationContext? previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new MarkedContext());
                try
                {
                    scenario.Outcome = await scenario.RunAsync(token);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
            })
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("on it"))
            .VerifyAsync();

    private sealed class MarkedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => ThreadPool.QueueUserWorkItem(_ =>
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        });
    }

    private sealed class DisposableContext(List<string> calls) : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
            calls.Add("disposed");
        }
    }

    private sealed class BrokenSink : IAssuranceEvidenceSink
    {
        public string Name => "broken";

        public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default) =>
            throw new IOException("Local report failure");
    }

    private sealed class Counter
    {
        public int Value { get; set; }
    }

    private sealed class CounterJourney
    {
        public Scenario<Counter> Build() =>
            Scenario.Define("Independent counter", () => new Counter(), BusinessContext.Define("Increment independent counters")
                    .Capability("c", "Capability")
                    .Outcome("o", "Outcome")
                    .Build(), id: "counter")
                .Given("A fresh counter", _ => { })
                .When("It is incremented", counter => counter.Value++)
                .Then("It was incremented once", counter => ExpectationResult.Check(counter.Value == 1, "Fresh state", "1", $"{counter.Value}"));
    }
}
