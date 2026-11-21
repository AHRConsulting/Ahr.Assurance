using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class InstanceStepTests
{
    private static readonly InstanceStepAnalyzer _analyzer = new();

    private const string Source = """
        public sealed class ListSteps : ScenarioSteps<List<string>>
        {
            public ScenarioAction<List<string>> Clear { get; } = CreateActionStep("Clear the list", list => list.Clear());
            public static ScenarioAction<List<string>> Fill { get; } = CreateActionStep("Fill the list", list => list.Add("item"));
        }

        public static class Tests
        {
            public static Task Run()
            {
                ListSteps steps = new();
                return Scenario.Define("Lists", () => new List<string>())
                    .Given(steps.Clear)
                    .When(ListSteps.Fill)
                    .Then("It holds one item", list => { })
                    .VerifyAsync();
            }
        }
        """;

    [Fact]
    public Task AnInstanceStepPropertyIsSuggestedAsStatic() =>
        Scenario.Define<CodeUnderAnalysis>("A step property in a steps class that isn't static is reported", AnalyzerRules.Mistakes,
                description: "A static step is shared by every scenario without creating the steps class.")
            .Given(TestCode("with a steps class whose 'Clear' step is an instance property", Source))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA108"))
            .VerifyAsync();

    [Fact]
    public Task TheFixMakesItStaticAndUpdatesItsUses() =>
        Scenario.Define<CodeUnderAnalysis>("The fix makes the step static and updates where it is used", AnalyzerRules.Mistakes)
            .Given(TestCode("with a steps class whose 'Clear' step is an instance property", Source))
            .When(TheFixIsApplied(_analyzer, new InstanceStepCodeFixProvider()))
            .Then(TheFixedCodeHas("public static ScenarioAction<List<string>> Clear { get; }"))
            .And(TheFixedCodeHas(".Given(ListSteps.Clear)"))
            .VerifyAsync();

    [Fact]
    public Task PropertiesOutsideStepsClassesAreNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("Step properties outside a steps class are not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("with an instance step property in a class that isn't a steps class", """
                public sealed class NotSteps
                {
                    public ScenarioAction<List<string>> Clear { get; } = null!;
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();
}
