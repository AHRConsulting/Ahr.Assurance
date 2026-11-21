using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Analyzers.Tests.AnalyzerSteps;

namespace Ahr.Assurance.Analyzers.Tests;

public sealed class StoredInlineScenarioTests
{
    [Fact]
    public Task FieldsAndPropertiesHoldingAnInlineScenarioAreReported() =>
        Scenario.Define<CodeUnderAnalysis>("A field or property that holds an inline scenario is reported", AnalyzerRules.Mistakes,
                description: "An inline scenario runs once; kept in a field, the second test to use it gets a spent scenario. A typed scenario is fine.")
            .Given(TestCode("with inline scenarios in a field and a property, and a typed one in a field", """
                public sealed class Tests
                {
                    private static readonly Scenario shared = Scenario.Define("Shared").Given("Ready", () => { });
                    public Scenario Template { get; } = Scenario.Define("Template");
                    private readonly Scenario<List<string>> typed = Scenario.Define("Typed", () => new List<string>());
                }
                """))
            .When(TheAnalyzerRuns(new StoredInlineScenarioAnalyzer()))
            .Then(ItIsReported("AHRA107", times: 2))
            .And(TheReportedCodeIs("shared", "Template"))
            .VerifyAsync();
}
