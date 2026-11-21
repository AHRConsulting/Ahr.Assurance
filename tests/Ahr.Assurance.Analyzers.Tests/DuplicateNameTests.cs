using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class DuplicateNameTests
{
    private static readonly DuplicateNameAnalyzer _analyzer = new();

    [Fact]
    public Task EveryScenarioSharingANameIsReported() =>
        Scenario.Define<CodeUnderAnalysis>("Every scenario that shares a name is reported", AnalyzerRules.Mistakes,
                description: "Two scenarios with one name are one journey in the report, so their results mix.")
            .Given(TestCode("with two scenarios named 'Same name' and one named 'Unique'", """
                public static class Tests
                {
                    public static Task First() => Scenario.Define("Same name").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    public static Task Second() => Scenario.Define("Same name").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    public static Task Unique() => Scenario.Define("Unique").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(ItIsReported("AHRA106", times: 2))
            .VerifyAsync();

    [Fact]
    public Task ExplicitIdsAndParameterisedTestsAreNotReported() =>
        Scenario.Define<CodeUnderAnalysis>("Shared names with their own IDs, or in parameterised tests, are not reported", AnalyzerRules.Mistakes)
            .Given(TestCode("with shared names that have IDs, and shared names in methods that take parameters", """
                public static class Tests
                {
                    public static Task First() => Scenario.Define("Same name", id: "first").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    public static Task Second() => Scenario.Define("Same name", id: "second").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    public static Task Row(int value) => Scenario.Define("A row").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                    public static Task OtherRow(string value) => Scenario.Define("A row").Given("Ready", () => { }).When("Act", () => { }).Then("Checked", () => { }).VerifyAsync();
                }
                """))
            .When(TheAnalyzerRuns(_analyzer))
            .Then(NothingIsReported)
            .VerifyAsync();
}
