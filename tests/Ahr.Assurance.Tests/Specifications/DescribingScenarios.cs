using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class DescribingScenarios
{
    [Theory]
    [InlineData(Satisfied)]
    [InlineData(Unsatisfied)]
    public Task DescriptionsAndKeywordsReachTheResultsAndTheReport(AssuranceStatus verdict) =>
        Scenario.Define<ScenarioUnderTest>("Descriptions and the keywords a scenario was written with reach its results and its report", ExecutionCapabilities.Describing)
            .Given($"A described scenario with two Givens and an And, whose check finds it {verdict}", scenario =>
            {
                ScenarioAction<object> setup = new("Setup", (_, _) => Task.CompletedTask, "Step <description>");
                ScenarioExpectation<object> observe = new("Observe", (_, _) => Task.FromResult(new ExpectationResult(verdict, "Observed explanation")), "Then description");
                scenario.Use(Scenario.Define("A described journey", () => new object(), Intent("Business intent <encoded>"), description: "Scenario <description>")
                    .Given(setup).Given(setup).And(setup).When(setup).Then(observe).And(observe));
            })
            .When(RunSteps.ItIsRun)
            .Then("The result keeps the description and each step's keyword as written", scenario =>
            {
                Assert.Equal("Scenario <description>", scenario.Result.Description);
                Assert.Equal([StepKeyword.Given, StepKeyword.Given, StepKeyword.And, StepKeyword.When, StepKeyword.Then, StepKeyword.And],
                    scenario.Result.Steps.Select(step => step.Keyword));
                Assert.All(scenario.Result.Steps.Take(3), step => Assert.Equal(StepKind.Given, step.Kind));
            })
            .And(ExecutionSteps.TheReportShows("Scenario <description>"))
            .And(ExecutionSteps.TheReportShows("Step <description>"))
            .And(ExecutionSteps.TheReportShows("Business intent <encoded>"))
            .And(ExecutionSteps.TheReportShows("Then description"))
            .And(ExecutionSteps.TheReportShows("Given Setup"))
            .And(ExecutionSteps.TheReportShows("And Setup"))
            .VerifyAsync();

    [Fact]
    public Task AScenarioWithoutABusinessContextIsReportedWithoutPlaceholders() =>
        Scenario.Define<ScenarioUnderTest>("A scenario without a business context runs and is reported without placeholders", ExecutionCapabilities.Describing)
            .Given("A scenario with no business context", scenario => scenario.Use(Scenario.Define("Without a business context", () => new object())
                .Given("Arrange", _ => { })
                .When("Act", _ => { })
                .Then("Observe", _ => new ExpectationResult(Satisfied, "Observed"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("Its result has no business context", scenario => Assert.Null(scenario.Result.Business))
            .And(ExecutionSteps.TheReportDoesNotShow("Not specified"))
            .And(ExecutionSteps.TheReportDoesNotShow("at-stake"))
            .VerifyAsync();

    [Fact]
    public Task AStoppedRunKeepsTheDescriptionsOfTheStepsThatDidNotRun() =>
        Scenario.Define<ScenarioUnderTest>("When a run stops, the steps that didn't run keep their descriptions and keywords", ExecutionCapabilities.Describing,
                description: "A reader of a stopped run still sees what the scenario was going to check.")
            .Given("A described scenario whose action fails before two described checks", scenario =>
            {
                ScenarioExpectation<object> observe = new("Observe", (_, _) => Task.FromResult(new ExpectationResult(Satisfied, "Observed")), "Expected <intent>");
                scenario.Use(Scenario.Define("A described journey that stops", () => new object(), Intent("Retain access after renewal"), description: "Scenario <intent>")
                    .Given(new ScenarioAction<object>("Setup", (_, _) => Task.CompletedTask))
                    .When(new ScenarioAction<object>("Unavailable operation", (_, _) => throw new IOException("service failed"), "Action <intent>"))
                    .Then(observe).And(observe));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("service failed"))
            .And("The stopped run keeps the description and the last step's And keyword", scenario =>
            {
                ScenarioResult result = scenario.Recorded;
                Assert.Equal(("Scenario <intent>", StepKeyword.And), (result.Description, result.Steps[^1].Keyword));
            })
            .And(ExecutionSteps.StepEndsAs(4, NotEvaluated, "Not run; execution stopped: service failed"))
            .And(ExecutionSteps.TheReportShows("Scenario <intent>"))
            .And(ExecutionSteps.TheReportShows("Action <intent>"))
            .And(ExecutionSteps.TheReportShows("Expected <intent>"))
            .And(ExecutionSteps.TheReportShows("And Observe"))
            .And(ExecutionSteps.TheReportShows("these results may be incomplete"))
            .VerifyAsync();

    [Fact]
    public Task TicketReferencesAreKeptWhenTheRunStops() =>
        Scenario.Define<ScenarioUnderTest>("A scenario's ticket references are kept even when its run stops", ExecutionCapabilities.Describing)
            .Given("A scenario linked to a ticket, whose action fails", scenario => scenario.Use(Scenario.Define("A referenced journey that stops", () => new object())
                .Reference("jira", "DOC-1", new Uri("https://example.test/DOC-1"))
                .Given("Ready", _ => { })
                .When("Breaks", (Action<object>)(_ => throw new InvalidOperationException("Broken")))
                .Then("Check", _ => new ExpectationResult(Satisfied, "Done"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("Broken"))
            .And(ExecutionSteps.ItIsLinkedTo("jira", "DOC-1"))
            .VerifyAsync();

    [Fact]
    public Task AReferenceAddedAfterTheStepsKeepsThem() =>
        Scenario.Define<ScenarioUnderTest>("A reference added after the steps keeps them", ExecutionCapabilities.Describing)
            .Given("A scenario whose ticket reference is added last", scenario => scenario.Use(Scenario.Define("A journey referenced last", () => new object())
                .Given("Ready", _ => { })
                .When("Act", _ => { })
                .Then("Check", _ => new ExpectationResult(Satisfied, "Done"))
                .Reference("jira", "DOC-1")))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("It ran all three steps", scenario => Assert.Equal(3, scenario.Result.Steps.Length))
            .And(ExecutionSteps.ItIsLinkedTo("jira", "DOC-1"))
            .VerifyAsync();

    [Fact]
    public Task AStepsNameIsTheTextItsAuthorWrites() =>
        Scenario.Define<ScenarioUnderTest>("A step's name is exactly the text its author writes", ExecutionCapabilities.Describing)
            .Given("A scenario whose step is given the type of the exception a caller catches", scenario => scenario.Use(
                Scenario.Define("A caller catches a missing argument", () => new object())
                    .Given("Ready", _ => { })
                    .When(CallerSteps.TheCallerCatches(typeof(ArgumentNullException)))
                    .Then("Done", _ => { })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.StepIsNamed(2, "The caller catches ArgumentNullException"))
            .And(RunSteps.StepRecords(2, ""))
            .VerifyAsync();

    private static BusinessContext Intent(string description) =>
        BusinessContext.Define(description).Capability("c", "Capability").Outcome("o", "Outcome").Build();

    private sealed class CallerSteps : ScenarioSteps<object>
    {
        public static ScenarioAction<object> TheCallerCatches(Type exception) =>
            CreateActionStep($"The caller catches {exception.Name}", _ => { });
    }
}
