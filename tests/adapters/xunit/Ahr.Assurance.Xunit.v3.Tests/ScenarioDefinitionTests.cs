using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class ScenarioDefinitionTests
{
    [Fact]
    public Task AScenarioDefinitionIsVerifiedThroughTheAdapter() =>
        Scenario.Define<VerificationUnderTest>("A scenario definition is verified through the adapter, with its report where it was asked for", XunitBehaviour.Reports)
            .Given("A counter journey, written as a scenario definition", verification => verification.Use(new CounterJourney().Build()))
            .And(AReportFile)
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("The report's location is returned", verification =>
                Assert.Equal(verification.ReportPath, Assert.Single(verification.Result!.Artifacts).Location))
            .And("The report is there", verification => Assert.True(File.Exists(verification.ReportPath)))
            .VerifyAsync();

    private sealed class Counter
    {
        public int Value { get; set; }
    }

    private sealed class CounterJourney
    {
        public Scenario<Counter> Build() =>
            Scenario.Define("Independent counter", _ => Task.FromResult(new Counter()), BusinessContext.Define("Increment independent counters")
                    .Capability("c", "Capability")
                    .Outcome("o", "Outcome")
                    .Build(), id: "counter")
            .Given(new("Fresh counter", (_, _) => Task.CompletedTask))
            .When(new("Increment", (context, _) =>
            {
                context.Value++;
                return Task.CompletedTask;
            }))
            .Then(new("Incremented once", (context, _) => Task.FromResult(
                ExpectationResult.Check(context.Value == 1, "Fresh state", "1", context.Value.ToString()))));
    }
}
