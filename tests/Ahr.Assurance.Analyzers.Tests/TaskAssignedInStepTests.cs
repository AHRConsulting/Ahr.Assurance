using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class TaskAssignedInStepTests
{
    private static readonly TaskAssignedInStepAnalyzer _analyzer = new();

    private const string Pending = """
        public static class Tests
        {
            public static Task Run()
            {
                Task? pending = null;
                return Scenario.Define("Pending work")
                    .Given("Work starts", () => pending = Task.Delay(10))
                    .When("It runs", () => { })
                    .Then("It is pending", () => { })
                    .VerifyAsync();
            }
        }
        """;

    [Fact]
    public Task AStepLambdaThatAssignsATaskIsReported() =>
        Scenario.Define<CodeUnderAnalysis>("A step that assigns a task in an expression body is reported", AnalyzerRules.Mistakes,
                description: "The assignment returns the task, so the step waits for work meant to run in the background.")
            .Given(TestCode("whose Given step is '() => pending = Task.Delay(10)'", Pending))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA101"))
            .And(TheMessageSays("'pending'"))
            .VerifyAsync();

    [Fact]
    public Task OtherLambdasAreNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("Block bodies, other values and awaited work are not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("with a block-bodied assignment, a count, awaits, and a lambda outside a step", """
                public static class Tests
                {
                    public static Task Run()
                    {
                        Task? pending = null;
                        var count = 0;
                        string? read = null;
                        Func<Task> elsewhere = () => pending = Task.CompletedTask;
                        return Scenario.Define("Pending work")
                            .Given("Work starts", () => { pending = Task.Delay(10); })
                            .When("A count is kept", () => count = 1)
                            .And("It awaits", async () => await Task.Yield())
                            .And("It reads", async token => read = await File.ReadAllTextAsync("a.txt", token))
                            .Then("It is pending", () => { })
                            .VerifyAsync();
                    }
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();

    [Fact]
    public Task TheFixUsesABlockBody() =>
        Scenario.Define<CodeUnderAnalysis>("The fix gives the step a block body", AnalyzerRules.Mistakes)
            .Given(TestCode("whose Given step is '() => pending = Task.Delay(10)'", Pending))
            .When(TheFixIsApplied(_analyzer, new TaskAssignedInStepCodeFixProvider()))
            .Then(TheFixedCodeHas(""".Given("Work starts", () => { pending = Task.Delay(10); })"""))
            .VerifyAsync();
}
