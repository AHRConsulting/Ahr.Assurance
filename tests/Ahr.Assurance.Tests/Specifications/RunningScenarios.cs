using System.Globalization;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using Ahr.Foundation;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class RunningScenarios
{
    private static readonly AsyncLocal<string?> _ambient = new();

    [Theory]
    [InlineData("an inline step")]
    [InlineData("an inline step with a token")]
    [InlineData("a typed step")]
    [InlineData("a reusable step")]
    [InlineData("a throws step")]
    public Task AStepsCultureAndAsyncLocalValuesReachTheStepsAfterIt(string form) =>
        Scenario.Define<ScenarioUnderTest>("A step's culture and async-local values reach the steps after it, as in a plain test", ExecutionCapabilities.Running,
                description: "A test that sets the culture in its setup reads the same culture in its checks, whichever way the step is written.")
            .Given($"A scenario that sets the culture and an async-local value in {form}, and reads them in a check", scenario => UseAmbient(form, scenario))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("fr-FR, set"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Theory]
    [InlineData("an inline scenario")]
    [InlineData("a typed scenario")]
    [InlineData("reusable steps")]
    public Task ASynchronousStepCanTakeTheRunsCancellationToken(string form) =>
        Scenario.Define<ScenarioUnderTest>("A synchronous step can take the run's cancellation token", ExecutionCapabilities.Running,
                description: "Work that accepts a token, such as a blocking call, is written without an async wrapper and still stops with the run.")
            .Given($"A scenario in {form} whose synchronous steps record whether they were given the run's token", scenario => UseTokenSteps(form, scenario))
            .When("The scenario is run with a token of its own", async (scenario, _) => scenario.Outcome = await scenario.RunAsync(scenario.Cancellation.Token))
            .Then(RunSteps.TheStepsRanInTheOrder("given: True, when: True, and: True, then: True"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AWorkflowActionStartsFromWhatTheCheckBeforeItLeft()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        return Scenario.Define<ScenarioUnderTest>("Each action in a workflow starts from the culture the steps before it left, and the test's own is kept", ExecutionCapabilities.Running)
            .Given("A workflow that changes the culture in each action", scenario => scenario.Use(Scenario.Define("Two cultures in turn")
                .Given("A French reader", () => CultureInfo.CurrentCulture = new("fr-FR"))
                .When("The page is read", () => scenario.Calls.Add(CultureInfo.CurrentCulture.Name))
                .Then("It is French", () => Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name))
                .When("The reader switches to German", () => CultureInfo.CurrentCulture = new("de-DE"))
                .Then("It is German", () => scenario.Calls.Add(CultureInfo.CurrentCulture.Name))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("fr-FR, de-DE"))
            .And("The test's own culture is unchanged after the run", _ => Assert.Equal(before, CultureInfo.CurrentCulture))
            .VerifyAsync();
    }

    [Theory]
    [InlineData(Satisfied)]
    [InlineData(Unsatisfied)]
    [InlineData(Inconclusive)]
    [InlineData(NotEvaluated)]
    public Task AScenarioTakesItsChecksVerdict(AssuranceStatus verdict) =>
        Scenario.Define<ScenarioUnderTest>("A scenario's verdict is its check's verdict, and its run still completes", ExecutionCapabilities.Running)
            .Given($"A scenario whose check finds it {verdict}", scenario => scenario.Use(Scenario.Define("A check reports its verdict")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("Evaluate", () => new ExpectationResult(verdict, "Observed verdict"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(verdict))
            .VerifyAsync();

    [Fact]
    public Task AnExpectedApplicationErrorCanSatisfyACheck() =>
        Scenario.Define<ScenarioUnderTest>("An error the application returns, when expected, satisfies a check", ExecutionCapabilities.Running,
                description: "A rejection the application is meant to return is a success for the scenario that expects it.")
            .Given("A scenario whose renewal returns a declined payment, as expected", scenario => scenario.Use(
                Scenario.Define("A declined renewal", () => new Renewal())
                    .Given("A customer whose payment will be declined", _ => { })
                    .When("The renewal is attempted", renewal => renewal.Outcome = Result<int, string>.Failure("payment-declined"))
                    .Then("The decline is returned", renewal => renewal.Outcome.Match(
                        _ => new ExpectationResult(Unsatisfied, "Unexpected success."),
                        error => ExpectationResult.Check(error == "payment-declined", "Expected decline", "payment-declined", error)))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.StepIs(3, Satisfied))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task EveryCheckRunsAfterOneFails() =>
        Scenario.Define<ScenarioUnderTest>("Every check runs after one fails, and the scenario takes the worst verdict", ExecutionCapabilities.Running,
                description: "Checks are independent, so one failure never hides what the others found.")
            .Given("A scenario with a failed assertion, then an inconclusive check, then a passing check", scenario => scenario.Use(Scenario.Define("Independent checks")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("A failed assertion", () => Assert.Fail("broken"))
                .And("An inconclusive check", () =>
                {
                    scenario.Calls.Add("inconclusive check ran");
                    return new ExpectationResult(Inconclusive, "Unknown");
                })
                .And("A later check", () => scenario.Calls.Add("later check ran"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("inconclusive check ran, later check ran"))
            .And(RunSteps.StepIs(3, Unsatisfied))
            .And(RunSteps.StepIs(4, Inconclusive))
            .And(RunSteps.StepIs(5, Satisfied))
            .And(RunSteps.TheScenarioIs(Unsatisfied))
            .VerifyAsync();

    [Fact]
    public Task AWorkflowChecksEachActionBeforeTheNext() =>
        Scenario.Define<ScenarioUnderTest>("A workflow checks the result of each action before taking the next", ExecutionCapabilities.Running,
                description: "A journey such as create, then change, then remove checks the state each action leaves.")
            .Given("A scenario that acts, checks, acts again and checks again", scenario => scenario.Use(Scenario.Define("Two actions, each checked")
                .Given("Ready", () => scenario.Calls.Add("ready"))
                .When("First action", () => scenario.Calls.Add("first action"))
                .Then("First check", () => scenario.Calls.Add("first check"))
                .When("Second action", () => scenario.Calls.Add("second action"))
                .Then("Second check", () => scenario.Calls.Add("second check"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("ready, first action, first check, second action, second check"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AFailedCheckStopsTheWorkflowBeforeItsNextAction() =>
        Scenario.Define<ScenarioUnderTest>("A failed check stops a workflow before its next action, after the other checks of that action run", ExecutionCapabilities.Running,
                description: "The next action would start from a state the checks already found wrong.")
            .Given("A workflow whose first check fails", scenario => scenario.Use(Scenario.Define("A first check fails")
                .Given("Ready", () => { })
                .When("First action", () => { })
                .Then("A failed check", () => Assert.Fail("broken"))
                .And("Another check", () => scenario.Calls.Add("other check ran"))
                .When("Second action", () => scenario.Calls.Add("second action ran"))
                .Then("Second check", () => { })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("other check ran"))
            .And(ExecutionSteps.StepEndsAs(5, NotEvaluated, "Not run; a check before this step failed."))
            .And(ExecutionSteps.StepEndsAs(6, NotEvaluated, "Not run; a check before this step failed."))
            .And(RunSteps.TheScenarioIs(Unsatisfied))
            .VerifyAsync();

    [Theory]
    [InlineData("an action with a token")]
    [InlineData("a named async lambda")]
    public Task AnAsynchronousStepIsAwaited(string form)
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return Scenario.Define<ScenarioUnderTest>("An asynchronous step is awaited before the next step runs", ExecutionCapabilities.Running)
            .Given($"A scenario whose step, written as {form}, waits to be released", scenario => scenario.Use(Holding(form, scenario, entered)))
            .When("The scenario is started and its step begins waiting", async (scenario, token) =>
            {
                scenario.Background = scenario.RunAsync(token);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            })
            .And("The step is released once the run is seen waiting for it", async scenario =>
            {
                var run = (Task<Result<AssuranceRunResult, AssuranceExecutionError>>)scenario.Background!;
                scenario.Calls.Add(run.IsCompleted ? "run finished early" : "run waited");
                scenario.Release.SetResult();
                scenario.Outcome = await run;
            })
            .Then(RunSteps.TheStepsRanInTheOrder("run waited, step finished, next step ran"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();
    }

    [Fact]
    public Task StepsSharedBetweenScenariosRunInEach() =>
        Scenario.Define<ScenarioUnderTest>("Steps shared between scenarios run in each, with a fresh context for each", ExecutionCapabilities.Running)
            .Given("Two scenarios made of the same counter steps", _ => { })
            .When("They are verified together", scenario =>
            {
                Scenario<TrackedCounter> first = Scenario.Define("A counter checked twice", () => new TrackedCounter(scenario.Calls))
                    .Given(TrackedCounterSteps.Ready).And(TrackedCounterSteps.AsyncSetup)
                    .When(TrackedCounterSteps.Increment)
                    .Then(TrackedCounterSteps.IncrementedOnce).And(TrackedCounterSteps.AsyncObservation);
                Scenario<TrackedCounter> second = Scenario.Define("A counter checked once", () => new TrackedCounter(scenario.Calls))
                    .Given(TrackedCounterSteps.Ready)
                    .When(TrackedCounterSteps.Increment)
                    .Then(TrackedCounterSteps.IncrementedOnce);
                return AssuranceAssert.VerifyAsync("Shared steps", [first, second], ExecutionSteps.Options(scenario));
            })
            .Then(RunSteps.TheStepsRanInTheOrder("created, incremented with the test's token, disposed, created, incremented with the test's token, disposed"))
            .And("Both scenarios are satisfied, each with its own steps", scenario =>
            {
                AssuranceRunResult run = scenario.Sink.Single.Report.Run;
                Assert.Equal(Satisfied, run.Status);
                Assert.Equal([5, 3], run.Scenarios.Select(result => result.Steps.Length));
            })
            .VerifyAsync();

    [Fact]
    public Task StepsFromAStepsClassGetTheTestsTokenAndAFreshContext() =>
        Scenario.Define<ScenarioUnderTest>("Steps from a steps class get the test's token, and each run's context is fresh and disposed", ExecutionCapabilities.Running)
            .Given("A described scenario made of a steps class's synchronous and asynchronous steps", scenario => scenario.Use(
                Scenario.Define("Counted by a steps class", () => new TrackedCounter(scenario.Calls), description: "Each run increments its own counter.")
                    .Given(TrackedCounterSteps.Ready)
                    .And(TrackedCounterSteps.AsyncSetup)
                    .When(TrackedCounterSteps.Increment)
                    .Then(TrackedCounterSteps.IncrementedOnce)
                    .And(TrackedCounterSteps.AsyncObservation)
                    .And(TrackedCounterSteps.TokenObservation)))
            .When(RunSteps.ItIsRun)
            .And(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("created, incremented with the test's token, disposed, created, incremented with the test's token, disposed"))
            .And("The run keeps the description, the And keyword and what the check found", scenario =>
                Assert.Equal(("Each run increments its own counter.", StepKeyword.And, "1"),
                    (scenario.Result.Description, scenario.Step(2).Keyword, scenario.Step(4).Evaluation.Actual)))
            .VerifyAsync();

    [Fact]
    public Task ChecksFromAStepsClassKeepAFailedVerdict() =>
        Scenario.Define<ScenarioUnderTest>("Checks from a steps class keep a failed verdict and what they found", ExecutionCapabilities.Running)
            .Given("A scenario that leaves its counter unchanged and checks it with a steps class's synchronous and asynchronous checks", scenario => scenario.Use(
                Scenario.Define("A counter left unchanged", () => new TrackedCounter(scenario.Calls))
                    .Given(TrackedCounterSteps.Ready)
                    .When("Leave the counter unchanged", _ => { })
                    .Then(TrackedCounterSteps.IncrementedOnce)
                    .And(TrackedCounterSteps.AsyncObservation)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .And(RunSteps.StepIs(3, Unsatisfied))
            .And(RunSteps.StepIs(4, Unsatisfied))
            .And("Both checks are recorded as checks that found 0", scenario => Assert.All(scenario.Result.Steps.Skip(2), step =>
                Assert.Equal((StepKind.Then, "0"), (step.Kind, step.Evaluation.Actual))))
            .VerifyAsync();

    [Fact]
    public Task AnAssertionInAWhenStepCanCheckARejection() =>
        Scenario.Define<ScenarioUnderTest>("An assertion in a When step can capture a rejection for the checks after it", ExecutionCapabilities.Running)
            .Given("A scenario whose When asserts that a call throws, and whose Then checks the exception", scenario =>
            {
                ArgumentNullException? rejection = null;
                scenario.Use(Scenario.Define("A rejection is checked the xUnit way")
                    .Given("A missing argument", () => { })
                    .When("The call is made", () => rejection = Assert.Throws<ArgumentNullException>(() => ArgumentNullException.ThrowIfNull((object?)null, "error")))
                    .Then("The rejection names the parameter", () => Assert.Equal("error", rejection!.ParamName)));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AFailedAssertionInAGivenStopsTheStepsAfterIt() =>
        Scenario.Define<ScenarioUnderTest>("A failed assertion in a Given fails the scenario, and the steps after it don't run", ExecutionCapabilities.Running,
                description: "An assertion in a Given is a precondition: when it fails, acting on it would prove nothing.")
            .Given("A scenario whose Given asserts a value that is missing", scenario => scenario.Use(Scenario.Define("A precondition fails")
                .Given("A value is present", () => Assert.NotNull(null))
                .When("Act", () => scenario.Calls.Add("when"))
                .Then("Check", () => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .And(ExecutionSteps.StepEndsAs(1, Unsatisfied, "xUnit assertion failed"))
            .And(ExecutionSteps.StepEndsAs(2, NotEvaluated, "Not run; an earlier step failed."))
            .And(ExecutionSteps.StepEndsAs(3, NotEvaluated, "Not run; an earlier step failed."))
            .And("Nothing after it ran", scenario => Assert.Empty(scenario.Calls))
            .VerifyAsync();

    private static Scenario<object> Holding(string form, ScenarioUnderTest scenario, TaskCompletionSource entered) =>
        form == "an action with a token"
            ? Scenario.Define("An action step that waits", () => new object())
                .Given("Ready", _ => { })
                .When(new ScenarioAction<object>("Wait for the operation", async (_, token) =>
                {
                    entered.SetResult();
                    await scenario.Release.Task.WaitAsync(token);
                    scenario.Calls.Add("step finished");
                }))
                .Then("The operation is observed", _ => scenario.Calls.Add("next step ran"))
            : Scenario.Define("A named async step that waits", () => new object())
                .Given("The asynchronous setup completes", async _ =>
                {
                    entered.SetResult();
                    await scenario.Release.Task;
                    scenario.Calls.Add("step finished");
                })
                .When("The setup is observed", _ => scenario.Calls.Add("next step ran"))
                .Then("Done", _ => { });

    private sealed class Renewal
    {
        public Result<int, string> Outcome { get; set; } = Result<int, string>.Success(0);
    }

    private static void SetAmbient()
    {
        CultureInfo.CurrentCulture = new("fr-FR");
        _ambient.Value = "set";
    }

    private static void ReadAmbient(ScenarioUnderTest scenario)
    {
        scenario.Calls.Add(CultureInfo.CurrentCulture.Name);
        scenario.Calls.Add(_ambient.Value ?? "not set");
    }

    private static void UseAmbient(string form, ScenarioUnderTest scenario)
    {
        if (form == "an inline step")
        {
            scenario.Use(Scenario.Define("Ambient values flow")
                .Given("The culture is set", SetAmbient)
                .When("Act", () => { })
                .Then("They are read", () => ReadAmbient(scenario)));
            return;
        }

        if (form == "an inline step with a token")
        {
            scenario.Use(Scenario.Define("Ambient values flow")
                .Given("The culture is set", _ =>
                {
                    SetAmbient();
                    return Task.CompletedTask;
                })
                .When("Act", () => { })
                .Then("They are read", () => ReadAmbient(scenario)));
            return;
        }

        if (form == "a throws step")
        {
            scenario.Use(Scenario.Define("Ambient values flow", () => new List<string>())
                .Given("Ready", _ => { })
                .WhenThrows<InvalidOperationException>("The culture is set, then the work is refused", _ =>
                {
                    SetAmbient();
                    throw new InvalidOperationException("refused");
                })
                .Then("They are read", _ => ReadAmbient(scenario)));
            return;
        }

        scenario.Use(Scenario.Define("Ambient values flow", () => new List<string>())
            .Given(TypedAmbient(form))
            .When("Act", _ => { })
            .Then("They are read", _ => ReadAmbient(scenario)));
    }

    private static ScenarioAction<List<string>> TypedAmbient(string form)
    {
        if (form == "a reusable step")
        {
            return AmbientSteps.TheCultureIsSet;
        }

        return ScenarioAction<List<string>>.Synchronous("The culture is set", _ => SetAmbient());
    }

    private sealed class AmbientSteps : ScenarioSteps<List<string>>
    {
        public static ScenarioAction<List<string>> TheCultureIsSet { get; } = CreateActionStep("The culture is set", _ => SetAmbient());
    }

    private static void UseTokenSteps(string form, ScenarioUnderTest scenario)
    {
        void Record(string step, CancellationToken token) => scenario.Calls.Add($"{step}: {token == scenario.Cancellation.Token}");
        if (form == "an inline scenario")
        {
            scenario.Use(Scenario.Define("Steps take the token")
                .Given("Ready", token => Record("given", token))
                .When("Act", token => Record("when", token))
                .And("Act again", token => Record("and", token))
                .Then("Check", token => Record("then", token)));
            return;
        }

        if (form == "a typed scenario")
        {
            scenario.Use(Scenario.Define("Steps take the token", () => new object())
                .Given("Ready", (_, token) => Record("given", token))
                .When("Act", (_, token) => Record("when", token))
                .And("Act again", (_, token) => Record("and", token))
                .Then("Check", (_, token) => Record("then", token)));
            return;
        }

        scenario.Use(Scenario.Define("Steps take the token", () => new TokenRecord(scenario))
            .Given(TokenSteps.Records("given"))
            .When(TokenSteps.Records("when"))
            .And(TokenSteps.Records("and"))
            .Then(TokenSteps.Checks("then")));
    }

    private sealed class TokenRecord(ScenarioUnderTest scenario)
    {
        public void Record(string step, CancellationToken token) => scenario.Calls.Add($"{step}: {token == scenario.Cancellation.Token}");
    }

    private sealed class TokenSteps : ScenarioSteps<TokenRecord>
    {
        public static ScenarioAction<TokenRecord> Records(string step) =>
            CreateActionStep(step, (record, token) => record.Record(step, token));

        public static ScenarioExpectation<TokenRecord> Checks(string step) =>
            CreateAssertionStep(step, (record, token) => record.Record(step, token));
    }
}
