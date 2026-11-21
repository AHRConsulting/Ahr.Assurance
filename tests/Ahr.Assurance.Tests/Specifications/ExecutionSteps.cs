using System.Globalization;
using System.Net;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests.Specifications;

internal sealed class ExecutionSteps : ScenarioSteps<ScenarioUnderTest>
{
    public static ScenarioAction<ScenarioUnderTest> ItIsRunAndCancelled { get; } =
        CreateThrowsStep<OperationCanceledException>("The scenario is run with the test's cancellation", scenario => scenario.RunAsync(scenario.Cancellation.Token));

    public static ScenarioExpectation<ScenarioUnderTest> ItIsRejectedBeforeItStarts(string message) =>
        CreateAssertionStep($"The scenario is rejected before it starts, saying '{message}'", scenario =>
        {
            Assert.Equal("invalid-definition", scenario.Error.Code);
            Assert.Contains(message, scenario.Error.Message);
            Assert.Empty(scenario.Calls);
        });

    public static ScenarioExpectation<ScenarioUnderTest> TheVerificationFails(string message) =>
        CreateThrownStep<global::Xunit.Sdk.XunitException>($"The verification fails, saying '{message}'", (_, failure) => Assert.Contains(message, failure.Message));

    public static ScenarioExpectation<ScenarioUnderTest> StepEndsAs(int number, AssuranceStatus status, string explanation) =>
        CreateAssertionStep($"Step {number} is {status}, saying '{explanation}'", scenario =>
        {
            ExpectationResult evaluation = scenario.Recorded.Steps[number - 1].Evaluation;
            Assert.Equal(status, evaluation.Status);
            Assert.StartsWith(explanation, evaluation.Explanation);
        });

    public static ScenarioExpectation<ScenarioUnderTest> ItIsLinkedTo(string provider, string key) =>
        CreateAssertionStep($"The scenario is linked to {provider} ticket {key}", scenario =>
        {
            ExternalReference reference = Assert.Single(scenario.Recorded.References);
            Assert.Equal((provider, key), (reference.Provider, reference.Key));
        });

    public static ScenarioExpectation<ScenarioUnderTest> TheReportShows(string text) =>
        CreateAssertionStep($"The report shows '{text}'", scenario =>
        {
            var html = Report(scenario);
            var encoded = WebUtility.HtmlEncode(text);
            Assert.Contains(encoded, html);
            if (encoded != text)
            {
                Assert.DoesNotContain(text, html);
            }
        });

    public static ScenarioExpectation<ScenarioUnderTest> TheReportDoesNotShow(string text) =>
        CreateAssertionStep($"The report does not show '{text}'", scenario => Assert.DoesNotContain(text, Report(scenario)));

    internal static ReportOptions Options(ScenarioUnderTest scenario) => new()
    {
        DefaultOutputDirectory = scenario.Output,
        Sinks = [scenario.Sink],
        TextOutput = AssuranceTextVerbosity.Off
    };

    private static string Report(ScenarioUnderTest scenario)
    {
        AssuranceReport report = scenario.Stopped
            ? new(scenario.PartialRun, [new(scenario.Error.Code, scenario.Error.Message)])
            : new(scenario.Run, []);
        return new StakeholderReportWriter().Render(report, "Report", developer: new(""));
    }
}

internal sealed class TrackedCounter : IDisposable
{
    public TrackedCounter(List<string> calls)
    {
        Calls = calls;
        Calls.Add("created");
    }

    public List<string> Calls { get; }

    public int Value { get; set; }

    public CancellationToken Token { get; set; }

    public void Dispose() => Calls.Add("disposed");
}

internal sealed class TrackedCounterSteps : ScenarioSteps<TrackedCounter>
{
    public static ScenarioAction<TrackedCounter> Ready { get; } = CreateActionStep("Ready", counter => counter.Value = 0);

    public static ScenarioAction<TrackedCounter> AsyncSetup { get; } = CreateActionStep("Async setup", async counter =>
    {
        await Task.Yield();
        counter.Value = 0;
    });

    public static ScenarioAction<TrackedCounter> Increment { get; } = CreateActionStep("Increment", (counter, token) =>
    {
        counter.Token = token;
        counter.Calls.Add(token == TestContext.Current.CancellationToken ? "incremented with the test's token" : "incremented with another token");
        counter.Value++;
        return Task.CompletedTask;
    });

    public static ScenarioExpectation<TrackedCounter> IncrementedOnce { get; } = CreateExpectationStep("Incremented once", counter =>
        ExpectationResult.Check(counter.Value == 1, "Fresh state", "1", counter.Value.ToString(CultureInfo.InvariantCulture)));

    public static ScenarioExpectation<TrackedCounter> AsyncObservation { get; } = CreateExpectationStep("Async observation", async counter =>
    {
        await Task.Yield();
        return ExpectationResult.Check(counter.Value == 1, "Awaited observation", "1", counter.Value.ToString(CultureInfo.InvariantCulture));
    });

    public static ScenarioExpectation<TrackedCounter> TokenObservation { get; } = CreateExpectationStep("Token observation", (counter, token) =>
        Task.FromResult(ExpectationResult.Check(counter.Token == token, "Caller token", "Same token",
            counter.Token == token ? "Same token" : "Different token")));

    public static ScenarioAction<TrackedCounter> BrokenAsyncSetup { get; } = CreateActionStep("Broken setup", async _ =>
    {
        await Task.Yield();
        throw new IOException("Setup failed");
    });
}
