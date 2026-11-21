using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class StepOrderTests
{
    private static readonly StepOrderAnalyzer _analyzer = new();

    [Theory]
    [InlineData(""".When("Act", () => { }).Given("Ready", () => { }).Then("Checked", () => { })""", "has a Given step after a When step")]
    [InlineData(""".Given("Ready", () => { }).Then("Checked", () => { })""", "has no When step")]
    [InlineData(""".Given("Ready", () => { }).When("Act", () => { })""", "has no Then step")]
    [InlineData(""".When("Act", () => { }).Then("Checked", () => { }).When("Act again", () => { })""", "has no Then step after its last When step")]
    [InlineData(""".WhenThrows<ArgumentNullException>("Act", () => { }).And("Act again", () => { })""", "has no Then step after its last When step")]
    [InlineData(""".Then("Checked", () => { }).When("Act", () => { }).Then("Checked again", () => { })""", "starts with a Then step")]
    [InlineData(""".Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).Given("Ready again", () => { }).When("Act again", () => { }).Then("Checked again", () => { })""", "has a Given step after a Then step")]
    public Task OutOfOrderOrMissingPhasesAreReported(string steps, string problem) =>
        Scenario.Define<CodeUnderAnalysis>("A scenario whose steps are out of order or missing a phase is reported", AnalyzerRules.Mistakes,
                description: "Given, When and Then run in order, and a scenario needs an action and a check after it.")
            .Given(TestCode($"whose scenario {problem}", $$"""
                public static class Tests
                {
                    public static Task Run() => Scenario.Define("Broken"){{steps}}.VerifyAsync();
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA105"))
            .And(TheMessageSays(problem))
            .VerifyAsync();

    [Fact]
    public Task CompleteChainsWorkflowsAndBuiltUpScenariosAreNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("Complete chains, workflows and scenarios built in parts are not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("with complete, throwing, workflow and built-up scenarios", """
                public static class Tests
                {
                    public static Task Complete() => Scenario.Define("Complete")
                        .Given("Ready", () => { }).And("Also ready", () => { })
                        .When("Act", () => { }).And("Act again", () => { })
                        .Then("Checked", () => { }).And("Checked again", () => { })
                        .VerifyAsync();

                    public static Task Throws() => Scenario.Define("Throws")
                        .Given("Ready", () => { })
                        .WhenThrows<ArgumentNullException>("Act", () => { }).AndThrows<InvalidOperationException>("Act again", () => { })
                        .Then("Checked", (ArgumentNullException e) => { }).And("Checked again", (InvalidOperationException e) => { })
                        .VerifyAsync();

                    public static Task ThrowsAny() => Scenario.Define("Throws any")
                        .Given("Ready", () => { })
                        .WhenThrowsAny<ArgumentException>("Act", () => { }).AndThrowsAny<OperationCanceledException>("Act again", () => { })
                        .Then("Checked", (ArgumentException e) => { }).And("Checked again", (OperationCanceledException e) => { })
                        .VerifyAsync();

                    public static Task StartsAtWhen() => Scenario.Define("Starts at When")
                        .When("Act", () => { }).Then("Checked", () => { })
                        .VerifyAsync();

                    public static Task EndsWithThrows() => Scenario.Define("Ends with a throws step")
                        .When("Act", () => { }).Then("Checked", () => { })
                        .WhenThrows<ArgumentNullException>("Act again", () => { }).AndThrowsAny<ArgumentException>("Act once more", () => { })
                        .VerifyAsync();

                    public static Task EndsWithABuiltStep(ScenarioAction<object> step) => Scenario.Define<object>("Ends with a built step")
                        .When(step)
                        .VerifyAsync();

                    public static Task Workflow() => Scenario.Define("Workflow")
                        .Given("Ready", () => { })
                        .When("Act", () => { }).Then("Checked", () => { }).And("Checked again", () => { })
                        .When("Act again", () => { }).Then("Checked after", () => { })
                        .VerifyAsync();

                    public static Task BuiltUp()
                    {
                        Scenario start = Scenario.Define("Built up").Given("Ready", () => { });
                        return start.When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    }
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();
}
