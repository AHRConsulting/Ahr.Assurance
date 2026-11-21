using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Adapters.Conformance;

/// <summary>Steps shared by every adapter. Checks fail with the framework's own assertion failure.</summary>
public sealed class AdapterSteps : ScenarioSteps<AdapterUnderTest>
{
    public static ScenarioAction<AdapterUnderTest> ItIsVerified { get; } =
        CreateActionStep("The scenario is verified through the adapter", (adapter, token) => adapter.VerifyAsync(token));

    public static ScenarioExpectation<AdapterUnderTest> TheTestPasses { get; } =
        CreateAssertionStep("The test passes", adapter =>
            adapter.Expect(adapter.Failure is null, $"Expected the test to pass, but it failed with {adapter.Failure?.GetType().Name}: {adapter.Failure?.Message}"));

    public static ScenarioExpectation<AdapterUnderTest> TheTestFailsNatively { get; } =
        CreateAssertionStep("The test fails the way the framework fails a test", adapter =>
            adapter.Expect(adapter.Failure is not null && adapter.Driver.IsNativeFailure(adapter.Failure),
                $"Expected the framework's own failure, got {Describe(adapter.Failure)}."));

    public static ScenarioExpectation<AdapterUnderTest> TheTestIsSkippedNatively { get; } =
        CreateAssertionStep("The test is skipped the way the framework skips a test", adapter =>
            adapter.Expect(adapter.Failure is not null && adapter.Driver.IsNativeSkip(adapter.Failure),
                $"Expected the framework's own skip, got {Describe(adapter.Failure)}."));

    public static ScenarioExpectation<AdapterUnderTest> TheVerificationIsCanceled { get; } =
        CreateAssertionStep("The verification stops as canceled", adapter =>
            adapter.Expect(adapter.Failure is OperationCanceledException, $"Expected cancellation, got {Describe(adapter.Failure)}."));

    public static ScenarioExpectation<AdapterUnderTest> TheFailureSays(string text) =>
        CreateAssertionStep($"The failure says '{text}'", adapter =>
            adapter.Expect(adapter.Failure?.Message.Contains(text, StringComparison.Ordinal) == true,
                $"Expected the failure to say '{text}', but it said: {adapter.Failure?.Message}"));

    public static ScenarioExpectation<AdapterUnderTest> TheVerdictIs(AssuranceStatus status) =>
        CreateAssertionStep($"The verdict is {status}", adapter =>
            adapter.Expect(adapter.Evidence.Report.Run.Status == status, $"Expected {status}, got {adapter.Evidence.Report.Run.Status}."));

    public static ScenarioExpectation<AdapterUnderTest> TheExecutionIs(VerificationExecutionState state) =>
        CreateAssertionStep($"The execution is {state}", adapter =>
            adapter.Expect(adapter.Evidence.ExecutionState == state, $"Expected {state}, got {adapter.Evidence.ExecutionState}."));

    public static ScenarioExpectation<AdapterUnderTest> StepIs(int number, AssuranceStatus status) =>
        CreateAssertionStep($"Step {number} is {status}", adapter =>
            adapter.Expect(adapter.Step(number).Evaluation.Status == status, $"Expected step {number} to be {status}, got {adapter.Step(number).Evaluation.Status}."));

    public static ScenarioExpectation<AdapterUnderTest> StepSays(int number, string text) =>
        CreateAssertionStep($"Step {number} says '{text}'", adapter =>
            adapter.Expect(adapter.Step(number).Evaluation.Explanation.Contains(text, StringComparison.Ordinal),
                $"Expected step {number} to say '{text}', but it said: {adapter.Step(number).Evaluation.Explanation}"));

    public static ScenarioExpectation<AdapterUnderTest> TheStepRan(string step) =>
        CreateAssertionStep($"'{step}' ran", adapter =>
            adapter.Expect(adapter.Ran.Contains(step), $"Expected '{step}' to run. Ran: {string.Join(", ", adapter.Ran)}."));

    public static ScenarioExpectation<AdapterUnderTest> TheStepDidNotRun(string step) =>
        CreateAssertionStep($"'{step}' didn't run", adapter =>
            adapter.Expect(!adapter.Ran.Contains(step), $"Expected '{step}' not to run. Ran: {string.Join(", ", adapter.Ran)}."));

    private static string Describe(Exception? exception) =>
        exception is null ? "no exception" : $"{exception.GetType().FullName}: {exception.Message}";
}
