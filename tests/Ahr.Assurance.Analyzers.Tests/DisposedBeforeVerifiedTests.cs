using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class DisposedBeforeVerifiedTests
{
    private static readonly DisposedBeforeVerifiedAnalyzer _analyzer = new();

    private const string Returned = """
        public static class Tests
        {
            public static Task Run()
            {
                using var cancellation = new CancellationTokenSource();
                return Scenario.Define("Cancellable")
                    .Given("Ready", () => { })
                    .When("Act", () => cancellation.Cancel())
                    .Then("Checked", () => { })
                    .VerifyAsync();
            }
        }
        """;

    [Fact]
    public Task ReturningVerifyAsyncAfterAUsingDeclarationIsReported() =>
        Scenario.Define<CodeUnderAnalysis>("Returning VerifyAsync after a using declaration is reported", AnalyzerRules.Mistakes,
                description: "The resource is disposed when the method returns, before the scenario has used it.")
            .Given(TestCode("that returns VerifyAsync after 'using var cancellation'", Returned))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA103"))
            .And(TheMessageSays("'cancellation'"))
            .VerifyAsync();

    [Fact]
    public Task AUsingStatementIsReportedToo() =>
        Scenario.Define<CodeUnderAnalysis>("Returning VerifyAsync inside a using statement is reported", AnalyzerRules.Mistakes)
            .Given(TestCode("that returns VerifyAsync inside 'using (var stream = ...)'", """
                public static class Tests
                {
                    public static Task Run()
                    {
                        using (var stream = new MemoryStream())
                        {
                            return Scenario.Define("Stream").Given("Ready", () => { }).When("Act", () => stream.WriteByte(1)).Then("Checked", () => { }).VerifyAsync();
                        }
                    }
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA103"))
            .VerifyAsync();

    [Fact]
    public Task AwaitingOrNoResourceIsNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("An awaited scenario, or one with no resource, is not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("that awaits VerifyAsync inside a using, and returns one with no resource", """
                public static class Tests
                {
                    public static async Task Awaited()
                    {
                        using var cancellation = new CancellationTokenSource();
                        await Scenario.Define("Awaited").Given("Ready", () => { }).When("Act", () => cancellation.Cancel()).Then("Checked", () => { }).VerifyAsync();
                    }

                    public static Task NoResource() =>
                        Scenario.Define("Plain").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();

    [Fact]
    public Task TheFixMakesTheMethodAsyncAndAwaits() =>
        Scenario.Define<CodeUnderAnalysis>("The fix makes the method async and awaits the scenario", AnalyzerRules.Mistakes)
            .Given(TestCode("that returns VerifyAsync after 'using var cancellation'", Returned))
            .When(TheFixIsApplied(_analyzer, new DisposedBeforeVerifiedCodeFixProvider()))
            .Then(TheFixedCodeHas("public static async Task Run()"))
            .And(TheFixedCodeHas("""await Scenario.Define("Cancellable")"""))
            .And(TheFixedCodeHasNo("return Scenario"))
            .VerifyAsync();
}
