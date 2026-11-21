using System.Collections.Concurrent;
using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using static Ahr.Assurance.Adapters.Conformance.AdapterSteps;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Adapters.Conformance;

/// <summary>What an adapter's test project supplies so the shared behaviour checks can run under its framework.</summary>
public interface IAdapterConformanceDriver
{
    Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
        where TContext : notnull;

    /// <summary>Wraps a callback using the adapter's reusable assertion capture.</summary>
    ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
        where TContext : notnull;

    /// <summary>Throws what the framework's own assertion library throws on failure.</summary>
    void FailAssertion(string message);

    /// <summary>Whether this exception is the framework's native test failure.</summary>
    bool IsNativeFailure(Exception exception);

    /// <summary>Skips the current test the way the framework does while it runs.</summary>
    void SkipTest(string reason);

    /// <summary>Whether this exception is the framework's native signal for a skipped test.</summary>
    bool IsNativeSkip(Exception exception);

    /// <summary>Text captured for the current test so far.</summary>
    string CapturedOutput { get; }
}

/// <summary>
/// The behaviour every adapter guarantees, as scenarios. Each adapter's test project verifies every case through its own
/// framework, so each adapter's report shows the same journeys.
/// </summary>
public static class AdapterConformance
{
    private static readonly ImmutableDictionary<string, Func<IAdapterConformanceDriver, Scenario<AdapterUnderTest>>> _cases =
        new Dictionary<string, Func<IAdapterConformanceDriver, Scenario<AdapterUnderTest>>>
        {
            [nameof(PassingVerificationReturnsConfirmedArtifacts)] = PassingVerificationReturnsConfirmedArtifacts,
            [nameof(AssertionFailureIsAnUnsatisfiedExpectationAndANativeFailure)] = AssertionFailureIsAnUnsatisfiedExpectationAndANativeFailure,
            [nameof(OtherExceptionsAreExecutionErrorsWithPartialEvidence)] = OtherExceptionsAreExecutionErrorsWithPartialEvidence,
            [nameof(AssertionFailureInWhenIsUnsatisfiedAndStopsLaterSteps)] = AssertionFailureInWhenIsUnsatisfiedAndStopsLaterSteps,
            [nameof(SkipInsideAStepReachesTheRunnerAsASkip)] = SkipInsideAStepReachesTheRunnerAsASkip,
            [nameof(AMissingExpectedExceptionFailsTheTestNatively)] = AMissingExpectedExceptionFailsTheTestNatively,
            [nameof(CancellationBeforeExecutionPropagatesWithoutRunningSteps)] = CancellationBeforeExecutionPropagatesWithoutRunningSteps,
            [nameof(CancellationDuringExecutionPropagatesAndKeepsEvidence)] = CancellationDuringExecutionPropagatesAndKeepsEvidence,
            [nameof(DeliveryFailureFailsAPassingVerdict)] = DeliveryFailureFailsAPassingVerdict,
            [nameof(DeliveryFailureDoesNotHideAnUnsatisfiedVerdict)] = DeliveryFailureDoesNotHideAnUnsatisfiedVerdict,
            [nameof(CapturedOutputContainsTheScenarioAndCanBeDisabled)] = CapturedOutputContainsTheScenarioAndCanBeDisabled,
            [nameof(EvidenceCarriesTheCurrentTestIdentity)] = EvidenceCarriesTheCurrentTestIdentity,
            [nameof(ParallelVerificationsStayIsolated)] = ParallelVerificationsStayIsolated,
            [nameof(ContextsAreFreshAndDisposedEvenOnFailure)] = ContextsAreFreshAndDisposedEvenOnFailure,
        }.ToImmutableDictionary();

    public static IEnumerable<string> CaseNames => _cases.Keys.Order(StringComparer.Ordinal);

    /// <summary>Verifies one case through the adapter, with the report options the test project is configured with.</summary>
    public static Task RunAsync(string caseName, IAdapterConformanceDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        return driver.VerifyAsync(Case(caseName, driver), new ReportOptions());
    }

    /// <summary>The scenario for one case, to verify through the adapter.</summary>
    public static Scenario<AdapterUnderTest> Case(string caseName, IAdapterConformanceDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        return _cases[caseName](driver);
    }

    private static Scenario<AdapterUnderTest> PassingVerificationReturnsConfirmedArtifacts(IAdapterConformanceDriver driver) =>
        Define(driver, "A passing scenario passes the test and returns its evidence", AdapterBehaviour.Verdicts)
            .Given("A scenario whose check passes", adapter => adapter.Use(adapter.Simple(_ => { }, "Passes")))
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And(TheVerdictIs(Satisfied))
            .And("The evidence's location is returned", adapter =>
                adapter.Expect(adapter.Result!.Artifacts.Any(artifact => artifact.Name == adapter.Sink.Name), "Expected the sink's artifact to be returned."))
            .And(TheExecutionIs(VerificationExecutionState.Completed));

    private static Scenario<AdapterUnderTest> AssertionFailureIsAnUnsatisfiedExpectationAndANativeFailure(IAdapterConformanceDriver driver) =>
        Define(driver, "A failed check fails the test natively, and later checks still run", AdapterBehaviour.Verdicts,
                "A failed check is a finding about the system, so the checks after it still run and report their own findings.")
            .Given("A scenario whose first check fails and whose second check is independent", adapter => adapter.Use(
                adapter.Simple(_ => adapter.Driver.FailAssertion("broken expectation"), "First check fails")
                    .And(adapter.Driver.Assertion<object>("Independent check", _ => adapter.Ran.Enqueue("Independent check")))))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("broken expectation"))
            .And(TheStepRan("Independent check"))
            .And(TheExecutionIs(VerificationExecutionState.Completed))
            .And(TheVerdictIs(Unsatisfied));

    private static Scenario<AdapterUnderTest> OtherExceptionsAreExecutionErrorsWithPartialEvidence(IAdapterConformanceDriver driver) =>
        Define(driver, "Any other exception fails the test as an execution error, keeping the steps before it", AdapterBehaviour.Verdicts)
            .Given("A scenario whose action throws an exception that isn't an assertion", adapter => adapter.Use(
                Scenario.Define("Breaks", () => new object())
                    .Given("Ready", _ => { })
                    .When("Breaks", (Action<object>)(_ => throw new InvalidOperationException("not an assertion")))
                    .Then(adapter.Driver.Assertion<object>("Never runs", _ => { }))))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheExecutionIs(VerificationExecutionState.Failed))
            .And(StepIs(1, Satisfied))
            .And(StepIs(3, NotEvaluated));

    private static Scenario<AdapterUnderTest> AssertionFailureInWhenIsUnsatisfiedAndStopsLaterSteps(IAdapterConformanceDriver driver) =>
        Define(driver, "A failed check in a When fails the test and stops the steps after it", AdapterBehaviour.Verdicts,
                "The action didn't happen as the scenario needs, so the checks that follow would check nothing.")
            .Given("A scenario whose action fails a check", adapter => adapter.Use(
                Scenario.Define("A precondition fails", () => new object())
                    .Given("Ready", _ => { })
                    .When("Act", _ => adapter.Driver.FailAssertion("precondition not met"))
                    .Then(adapter.Driver.Assertion<object>("Never runs", _ => adapter.Ran.Enqueue("Never runs")))))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("precondition not met"))
            .And(TheStepDidNotRun("Never runs"))
            .And(TheExecutionIs(VerificationExecutionState.Completed))
            .And(StepIs(2, Unsatisfied))
            .And(StepIs(3, NotEvaluated));

    private static Scenario<AdapterUnderTest> SkipInsideAStepReachesTheRunnerAsASkip(IAdapterConformanceDriver driver) =>
        Define(driver, "A step that skips its test skips it natively, with its reason", AdapterBehaviour.Skips)
            .Given("A scenario whose Given skips because no database is available", adapter => adapter.Use(
                Scenario.Define("The environment is missing", () => new object())
                    .Given("A database is available", _ => adapter.Driver.SkipTest("no database here"))
                    .When("Act", _ => adapter.Ran.Enqueue("Act"))
                    .Then(adapter.Driver.Assertion<object>("Never runs", _ => { }))))
            .When(ItIsVerified)
            .Then(TheTestIsSkippedNatively)
            .And(TheStepDidNotRun("Act"))
            .And(StepIs(1, Inconclusive))
            .And(StepSays(1, "Skipped: no database here"))
            .And(StepIs(3, NotEvaluated));

    private static Scenario<AdapterUnderTest> AMissingExpectedExceptionFailsTheTestNatively(IAdapterConformanceDriver driver) =>
        Define(driver, "An exception that was expected but not thrown fails the test natively", AdapterBehaviour.Verdicts)
            .Given("A scenario that expects a rejection its action never makes", adapter => adapter.Use(
                Scenario.Define("A missing argument is rejected", () => new object())
                    .Given("A missing argument", _ => { })
                    .WhenThrows<ArgumentNullException>("The call is made", _ => { })
                    .Then<ArgumentNullException>("The rejection names the parameter", (_, rejection) =>
                        adapter.Expect(rejection.ParamName == "value", "never runs"))))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(StepIs(2, Unsatisfied))
            .And(StepSays(2, "Expected ArgumentNullException, but nothing was thrown."))
            .And(StepIs(3, NotEvaluated));

    private static Scenario<AdapterUnderTest> CancellationBeforeExecutionPropagatesWithoutRunningSteps(IAdapterConformanceDriver driver) =>
        Define(driver, "A run canceled before it starts runs no steps", AdapterBehaviour.Cancellation)
            .Given("A scenario whose check records that it ran", adapter => adapter.Use(adapter.Simple(_ => adapter.Ran.Enqueue("Check"), "Canceled early")))
            .And("The run is already canceled", adapter => adapter.Cancellation.CancelAsync())
            .When("The scenario is verified through the adapter with that cancellation", (adapter, _) => adapter.VerifyAsync(adapter.Cancellation.Token))
            .Then(TheVerificationIsCanceled)
            .And(TheStepDidNotRun("Check"));

    private static Scenario<AdapterUnderTest> CancellationDuringExecutionPropagatesAndKeepsEvidence(IAdapterConformanceDriver driver) =>
        Define(driver, "A run canceled part way stops, and its evidence keeps the steps before it", AdapterBehaviour.Cancellation)
            .Given("A scenario whose action cancels the run", adapter => adapter.Use(
                Scenario.Define("Canceled", () => new object())
                    .Given("Ready", _ => { })
                    .When("Cancels", async _ =>
                    {
                        await adapter.Cancellation.CancelAsync();
                        adapter.Cancellation.Token.ThrowIfCancellationRequested();
                    })
                    .Then(adapter.Driver.Assertion<object>("Never runs", _ => { }))))
            .When("The scenario is verified through the adapter with that cancellation", (adapter, _) => adapter.VerifyAsync(adapter.Cancellation.Token))
            .Then(TheVerificationIsCanceled)
            .And(TheExecutionIs(VerificationExecutionState.Canceled))
            .And(StepIs(1, Satisfied));

    private static Scenario<AdapterUnderTest> DeliveryFailureFailsAPassingVerdict(IAdapterConformanceDriver driver) =>
        Define(driver, "Evidence that can't be delivered fails a passing test", AdapterBehaviour.Delivery)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Undelivered pass")))
            .And("Its evidence destination is down", adapter => adapter.Sink = new(new IOException("destination down")))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively);

    private static Scenario<AdapterUnderTest> DeliveryFailureDoesNotHideAnUnsatisfiedVerdict(IAdapterConformanceDriver driver) =>
        Define(driver, "Evidence that can't be delivered doesn't hide a failed check", AdapterBehaviour.Delivery)
            .Given("A scenario whose check fails", adapter => adapter.Use(adapter.Simple(_ => adapter.Driver.FailAssertion("broken expectation"), "Undelivered failure")))
            .And("Its evidence destination is down", adapter => adapter.Sink = new(new IOException("destination down")))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("broken expectation"));

    private static Scenario<AdapterUnderTest> CapturedOutputContainsTheScenarioAndCanBeDisabled(IAdapterConformanceDriver driver)
    {
        const string hidden = "Scenario with text output off";
        const string visible = "Scenario with text output on";
        return Define(driver, "The test's output names the scenario, unless text output is off", AdapterBehaviour.Identity)
            .Given("Two passing scenarios, one to verify with text output off and one with it on", _ => { })
            .When("Both are verified through the adapter, and the test's output is read", async (adapter, token) =>
            {
                adapter.Use(adapter.Simple(_ => { }, hidden));
                await adapter.VerifyAsync(token);
                adapter.Use(adapter.Simple(_ => { }, visible));
                adapter.TextOutput = AssuranceTextVerbosity.Standard;
                await adapter.VerifyAsync(token);
                adapter.Output = adapter.Driver.CapturedOutput;
            })
            .Then("The output doesn't name the scenario verified with text output off", adapter =>
                adapter.Expect(!adapter.Output.Contains(hidden, StringComparison.Ordinal), "Off writes no captured text."))
            .And("The output names the scenario verified with text output on", adapter =>
                adapter.Expect(adapter.Output.Contains(visible, StringComparison.Ordinal), $"Expected the output to name the scenario. Output: {adapter.Output}"));
    }

    private static Scenario<AdapterUnderTest> EvidenceCarriesTheCurrentTestIdentity(IAdapterConformanceDriver driver) =>
        Define(driver, "Evidence names the test and the test assembly it came from", AdapterBehaviour.Identity)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Named")))
            .When(ItIsVerified)
            .Then("The evidence has the test's ID and name", adapter =>
                adapter.Expect(!string.IsNullOrWhiteSpace(adapter.Evidence.TestId) && !string.IsNullOrWhiteSpace(adapter.Evidence.TestName),
                    $"Expected a test ID and name, got '{adapter.Evidence.TestId}' and '{adapter.Evidence.TestName}'."))
            .And("The evidence's producer is the test assembly", adapter =>
                adapter.Expect(!string.IsNullOrWhiteSpace(adapter.Evidence.Producer) && adapter.Evidence.Producer != "verification",
                    $"Expected the test assembly, got '{adapter.Evidence.Producer}'."));

    private static Scenario<AdapterUnderTest> ParallelVerificationsStayIsolated(IAdapterConformanceDriver driver)
    {
        ConcurrentBag<int> contexts = [];
        return Define(driver, "Verifications running in parallel each keep their own context and evidence", AdapterBehaviour.Isolation)
            .Given("16 scenarios that record the context each one runs with", _ => { })
            .When("They are verified through the adapter at the same time", async (adapter, token) =>
                _ = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() => adapter.Driver.VerifyAsync(
                    Scenario.Define($"Parallel {index}", () => new object())
                        .Given("Ready", context => contexts.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(context)))
                        .When("Act", _ => { })
                        .Then(adapter.Driver.Assertion<object>("Check", _ => { })),
                    adapter.Options, token), token))))
            .Then("Every verification delivers its own evidence", adapter =>
                adapter.Expect(adapter.Sink.All.Count == 16 && adapter.Sink.All.Select(item => item.VerificationId).Distinct().Count() == 16,
                    $"Expected 16 pieces of evidence with distinct IDs, got {adapter.Sink.All.Count}."))
            .And("Every verification runs with its own context", adapter =>
                adapter.Expect(contexts.Distinct().Count() == 16, $"Expected 16 contexts, got {contexts.Distinct().Count()}."));
    }

    private static Scenario<AdapterUnderTest> ContextsAreFreshAndDisposedEvenOnFailure(IAdapterConformanceDriver driver)
    {
        List<DisposableContext> created = [];
        return Define(driver, "Each run gets a fresh context, disposed even when a check fails", AdapterBehaviour.Isolation)
            .Given("A scenario whose check fails", adapter => adapter.Use(
                Scenario.Define("Disposal", () =>
                {
                    DisposableContext context = new();
                    created.Add(context);
                    return context;
                })
                    .Given("Ready", _ => { })
                    .When("Act", _ => { })
                    .Then(adapter.Driver.Assertion<DisposableContext>("Fails", _ => adapter.Driver.FailAssertion("broken expectation")))))
            .When("The scenario is verified through the adapter twice", async (adapter, token) =>
            {
                await adapter.VerifyAsync(token);
                await adapter.VerifyAsync(token);
            })
            .Then("Each run created its own context", adapter =>
                adapter.Expect(created.Count == 2 && created[0] != created[1], $"Expected 2 distinct contexts, got {created.Count}."))
            .And("Both contexts are disposed", adapter =>
                adapter.Expect(created.All(context => context.Disposed), "Expected every context to be disposed after the failure."));
    }

    private static Scenario<AdapterUnderTest> Define(IAdapterConformanceDriver driver, string name, BusinessContext business, string? description = null) =>
        Scenario.Define(name, () => new AdapterUnderTest(driver), business, description: description);

    private sealed class DisposableContext : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
