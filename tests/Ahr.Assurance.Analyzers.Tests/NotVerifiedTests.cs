using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class NotVerifiedTests
{
    private static readonly NotVerifiedAnalyzer _analyzer = new();
    private static readonly NotVerifiedCodeFixProvider _fix = new();

    [Fact]
    public Task AScenarioWithoutVerifyAsyncIsReported() =>
        Scenario.Define<CodeUnderAnalysis>("A scenario that is never verified is reported", AnalyzerRules.Mistakes,
                description: "Without VerifyAsync the scenario never runs, and the test passes having checked nothing.")
            .Given(TestCode("that defines a scenario and never verifies it", """
                public static class Tests
                {
                    public static void Run()
                    {
                        Scenario.Define("Forgotten")
                            .Given("Ready", () => { })
                            .When("Act", () => { })
                            .Then("Checked", () => { });
                    }
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA102"))
            .VerifyAsync();

    [Fact]
    public Task AnUnawaitedVerifyAsyncIsReported() =>
        Scenario.Define<CodeUnderAnalysis>("A VerifyAsync that isn't awaited is reported", AnalyzerRules.Mistakes,
                description: "The test ends before the scenario does, so its result is lost.")
            .Given(TestCode("that calls VerifyAsync without awaiting it", """
                public static class Tests
                {
                    public static void Run()
                    {
                        Scenario.Define("Unawaited")
                            .Given("Ready", () => { })
                            .When("Act", () => { })
                            .Then("Checked", () => { })
                            .VerifyAsync();
                    }
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA102"))
            .And(TheMessageSays("isn't awaited"))
            .VerifyAsync();

    [Fact]
    public Task VerifiedAndDiscardedScenariosAreNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("Awaited, returned, discarded and kept scenarios are not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("that awaits, returns, discards and keeps scenarios", """
                public static class Tests
                {
                    public static async Task Run()
                    {
                        await Scenario.Define("Awaited").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                        _ = Scenario.Define("Discarded on purpose").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                        Scenario kept = Scenario.Define("Kept").Given("Ready", () => { });
                    }

                    public static Task Returned() =>
                        Scenario.Define("Returned").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();

    [Fact]
    public Task TheFixAwaitsVerifyAsyncInAnAsyncMethod() =>
        Scenario.Define<CodeUnderAnalysis>("The fix verifies and awaits a forgotten scenario in an async method", AnalyzerRules.Mistakes)
            .Given(TestCode("in an async method that never verifies its scenario", """
                public static class Tests
                {
                    public static async Task Run()
                    {
                        await Task.Yield();
                        Scenario.Define("Forgotten").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { });
                    }
                }
                """))
            .When(TheFixIsApplied(_analyzer, _fix))
            .Then(TheFixedCodeHas("""await Scenario.Define("Forgotten").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();"""))
            .VerifyAsync();

    [Fact]
    public Task TheFixAwaitsAnUnawaitedVerifyAsync() =>
        Scenario.Define<CodeUnderAnalysis>("The fix awaits a VerifyAsync that wasn't awaited", AnalyzerRules.Mistakes)
            .Given(TestCode("in an async method that calls VerifyAsync without awaiting it", """
                public static class Tests
                {
                    public static async Task Run()
                    {
                        await Task.Yield();
                        Scenario.Define("Unawaited").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    }
                }
                """))
            .When(TheFixIsApplied(_analyzer, _fix))
            .Then(TheFixedCodeHas("""await Scenario.Define("Unawaited")"""))
            .VerifyAsync();

    [Fact]
    public Task NoFixIsOfferedOutsideAnAsyncMethod() =>
        Scenario.Define<CodeUnderAnalysis>("No fix is offered outside an async method", AnalyzerRules.Mistakes,
                description: "Awaiting there would need the method changed, which the team decides.")
            .Given(TestCode("in a method that isn't async", """
                public static class Tests
                {
                    public static void Run() =>
                        Scenario.Define("Forgotten").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                }
                """))
            .When(TheFixIsAskedFor(_analyzer, _fix))
            .Then(NoFixIsOffered)
            .VerifyAsync();
}
