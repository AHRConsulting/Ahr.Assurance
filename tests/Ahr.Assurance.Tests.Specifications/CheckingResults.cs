using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class CheckingResults
{
    [Fact]
    public Task AWhensResultReachesTheChecksAfterIt() =>
        Scenario.Define<ScenarioUnderTest>("A When's result reaches the checks after it, typed", Capabilities.Results)
            .Given("A scenario whose When returns a basket's total and whose checks take it", scenario => scenario.Use(Scenario.Define("A basket adds up")
                .Given("A basket of two lines", () => { })
                .When("The total is calculated", () => 7m + 5m)
                .Then("It is 12", (decimal total) => Assert.Equal(12m, total))
                .And("It is recorded", (decimal total) => scenario.Calls.Add($"{total}"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("12"))
            .VerifyAsync();

    [Fact]
    public Task AnAsynchronousWhensResultReachesItsChecks() =>
        Scenario.Define<ScenarioUnderTest>("An asynchronous When's result reaches its checks, in a typed scenario", Capabilities.Results)
            .Given("A typed scenario whose When awaits a lookup with the run's token", scenario => scenario.Use(Scenario.Define("A customer is found", () => new Dictionary<string, string> { ["42"] = "Ada" })
                .Given("A customer with ID 42", _ => { })
                .When("The customer is looked up", async (customers, token) =>
                {
                    await Task.Delay(1, token);
                    return customers["42"];
                })
                .Then<string>("It is Ada", (_, name) => Assert.Equal("Ada", name))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task EachActionsChecksReadThatActionsResult() =>
        Scenario.Define<ScenarioUnderTest>("In a workflow, each action's checks read that action's result", Capabilities.Results)
            .Given("A workflow whose two actions each return a count", scenario => scenario.Use(Scenario.Define("A list grows and shrinks")
                .Given("A list of one", () => { })
                .When("An item is added", () => 2)
                .Then("There are two", (int count) => scenario.Calls.Add($"{count}"))
                .When("Both are removed", () => 0)
                .Then("There are none", (int count) => scenario.Calls.Add($"{count}"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("2, 0"))
            .VerifyAsync();

    [Fact]
    public Task ResultsAndExpectedExceptionsAreTakenTogether() =>
        Scenario.Define<ScenarioUnderTest>("A check can take a result while another takes an expected exception", Capabilities.Results)
            .Given("A scenario with a result and a throws step in one action, and a check of each", scenario => scenario.Use(Scenario.Define("A partial import")
                .Given("A file of three rows", () => { })
                .When("Two rows import", () => 2)
                .AndThrows<FormatException>("The third row is refused", () => throw new FormatException("row 3"))
                .Then("Two rows are in", (int imported) => scenario.Calls.Add($"{imported}"))
                .And("Row 3 is named", (FormatException rejection) => scenario.Calls.Add(rejection.Message))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("2, row 3"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ACheckOfAResultNoStepReturnsIsRefused() =>
        Scenario.Define<ScenarioUnderTest>("A check of a result that no step returns is refused before anything runs", Capabilities.Results)
            .Given("A scenario whose check takes a decimal that no step returns", scenario => scenario.Use(Scenario.Define("A total nobody calculated")
                .Given("A basket", () => scenario.Calls.Add("ran"))
                .When("Nothing is calculated", () => { })
                .Then("It is 12", (decimal total) => Assert.Equal(12m, total))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("'It is 12' takes the decimal that an earlier step returns, but no earlier step returns one."))
            .And(RunSteps.TheStepsRanInTheOrder(""))
            .VerifyAsync();

    [Fact]
    public Task ANullResultIsStillAResult() =>
        Scenario.Define<ScenarioUnderTest>("A When that returns null still hands its result to the checks after it", Capabilities.Results)
            .Given("A scenario whose When returns a null string and whose check takes it", scenario => scenario.Use(Scenario.Define("A missing name")
                .Given("A customer without a name", () => { })
                .When("The name is read", Nothing)
                .Then("There is none", (string? name) => Assert.Null(name))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AnAndActionGivesItsResult() =>
        Scenario.Define<ScenarioUnderTest>("An And action that returns a value hands it to the checks after it", Capabilities.Results)
            .Given("A workflow whose second action adds an item that is already there, in an And", scenario =>
            {
                HashSet<int> seen = [];
                scenario.Use(Scenario.Define("An item is added twice")
                    .Given("An empty set", () => { })
                    .When("The item is added", () => seen.Add(1))
                    .Then("It was new", (bool added) => Assert.True(added))
                    .When("The set is counted", () => seen.Count)
                    .And("The item is added again", () => seen.Add(1))
                    .Then("It was not new", (bool added) => Assert.False(added))
                    .And("There is one item", (int count) => Assert.Equal(1, count)));
            })
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ACheckOfAnEarlierActionsResultIsRefused() =>
        Scenario.Define<ScenarioUnderTest>("A check that would read an earlier action's result is refused before anything runs", Capabilities.Results)
            .Given("A workflow whose second check takes a count that only the first action returns", scenario => scenario.Use(Scenario.Define("A stale count")
                .Given("A list", () => scenario.Calls.Add("ran"))
                .When("An item is added", () => 1)
                .Then("There is one", (int count) => Assert.Equal(1, count))
                .When("The list is cleared", () => { })
                .Then("There are none", (int count) => Assert.Equal(0, count))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("'There are none' takes the int that a step in its When and Then block returns, but none there returns one. "
                + "'An item is added' returns one in an earlier block; check it in that block, or return it again in this one."))
            .And(RunSteps.TheStepsRanInTheOrder(""))
            .VerifyAsync();

    [Fact]
    public Task ACheckOfAStructNamesTheStepThatReturnsItsNullableForm() =>
        Scenario.Define<ScenarioUnderTest>("A check of a struct that a step returns as nullable is refused with that step named", Capabilities.Results)
            .Given("A workflow whose action returns an int? and whose check takes an int", scenario => scenario.Use(Scenario.Define("A maybe count")
                .Given("A list", () => scenario.Calls.Add("ran"))
                .When("It is counted", () => (int?)1)
                .Then("There is one", (int count) => Assert.Equal(1, count))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("'There is one' takes the int that a step in its When and Then block returns, but none there returns one. "
                + "'It is counted' returns int?, a different type; take int? in the check, or return the value itself from the step."))
            .And(RunSteps.TheStepsRanInTheOrder(""))
            .VerifyAsync();

    [Fact]
    public Task ChecksOfAResultCanTakeTheRunsToken() =>
        Scenario.Define<ScenarioUnderTest>("Checks of a result can take the run's cancellation token", Capabilities.Results)
            .Given("A scenario whose checks take the result and the token", scenario => scenario.Use(Scenario.Define("A count")
                .Given("A list of two", () => { })
                .When("It is counted", () => 2)
                .Then("It is two", (int count, CancellationToken token) => scenario.Calls.Add($"{count} {token.CanBeCanceled}"))
                .And("It is still two", async (int count, CancellationToken token) =>
                {
                    await Task.Delay(1, token);
                    scenario.Calls.Add($"{count}");
                })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("2 True, 2"))
            .VerifyAsync();

    [Fact]
    public Task StepsClassesGiveAndCheckResults() =>
        Scenario.Define<ScenarioUnderTest>("A steps class gives a result from an action and checks it, with values and the run's token", Capabilities.Results)
            .Given("A typed scenario built only from basket steps", scenario => scenario.Use(Scenario.Define<Basket>("A basket of two lines adds up")
                .Given(BasketSteps.ALineIsAdded(7m))
                .And(BasketSteps.ALineIsAdded(5m))
                .When(BasketSteps.TheTotalIsLookedUp)
                .Then(BasketSteps.ItIs(12m))
                .And(BasketSteps.ItIsPositive)
                .When(BasketSteps.TheLinesAreCounted)
                .Then(BasketSteps.ThereAre(2))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepIsNamed(4, "It is 12"))
            .VerifyAsync();

    [Fact]
    public Task AStepMethodGivesItsResult() =>
        Scenario.Define<ScenarioUnderTest>("A step method that takes values gives what its work returns", Capabilities.Results)
            .Given("A typed scenario whose actions take values and return counts", scenario => scenario.Use(Scenario.Define<Basket>("A basket grows")
                .Given(BasketSteps.ALineIsAdded(7m))
                .When(BasketSteps.ALineIsAddedAndCounted(5m))
                .Then(BasketSteps.ThereAre(2))
                .When(BasketSteps.LinesAreAddedAndCounted(1m, 2m))
                .Then(BasketSteps.ThereAre(4))
                .When(BasketSteps.LinesAreAddedAndCountedLater(3m, 4m, 6m))
                .Then(BasketSteps.ThereAre(7))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepIsNamed(2, "A line of 5 is added and counted"))
            .VerifyAsync();

    [Fact]
    public Task AValueTaskIsAwaitedAndGivesItsResult() =>
        Scenario.Define<ScenarioUnderTest>("A step that returns a ValueTask is awaited, and gives what it completes with", Capabilities.Results)
            .Given("A workflow whose actions return a ValueTask and a ValueTask of a recorded exception", scenario => scenario.Use(Scenario.Define("A failure is recorded")
                .Given("A list", () => { })
                .When("The work runs later", () => LaterAsync(scenario))
                .And("The failure is recorded", () => RecordAsync(() => throw new InvalidOperationException("refused")))
                .Then("It is the refusal", (Exception? failure) => scenario.Calls.Add(Assert.IsType<InvalidOperationException>(failure).Message))
                .When("A typed count comes later", () => new ValueTask<int>(3))
                .Then("It is three", (int count) => scenario.Calls.Add($"{count}"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("awaited, refused, 3"))
            .VerifyAsync();

    private static async ValueTask LaterAsync(ScenarioUnderTest scenario)
    {
        await Task.Yield();
        scenario.Calls.Add("awaited");
    }

    private static async ValueTask<Exception?> RecordAsync(Action work)
    {
        await Task.Yield();
        try
        {
            work();
            return null;
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    [Fact]
    public Task SetupKeepsNoResult() =>
        Scenario.Define<ScenarioUnderTest>("What a Given step returns is setup, not a result its checks read", Capabilities.Results)
            .Given("A scenario whose Given steps return counts, and whose When returns the count its check takes", scenario => scenario.Use(Scenario.Define<Basket>("A basket is counted")
                .Given(BasketSteps.TheLinesAreCounted)
                .And("A line of 3 is added", basket =>
                {
                    basket.Lines.Add(3m);
                    return basket.Lines.Count;
                })
                .When("The lines are counted again", basket => basket.Lines.Count + 1)
                .Then<int>("There are two", (_, count) => Assert.Equal(2, count))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    private static string? Nothing() => null;

    private sealed class Basket
    {
        public List<decimal> Lines { get; } = [];
    }

    private sealed class BasketSteps : ScenarioSteps<Basket>
    {
        public static ScenarioAction<Basket> ALineIsAdded(decimal price) => CreateActionStep($"A line of {price} is added", (basket, token) =>
        {
            token.ThrowIfCancellationRequested();
            basket.Lines.Add(price);
        });

        public static ScenarioAction<Basket> TheTotalIsLookedUp { get; } = CreateActionStep("The total is looked up", async (basket, token) =>
        {
            await Task.Delay(1, token);
            return basket.Lines.Sum();
        });

        public static ScenarioAction<Basket> ALineIsAddedAndCounted(decimal price) =>
            CreateActionStep($"A line of {price} is added and counted", async (basket, token) =>
            {
                await Task.Delay(1, token);
                basket.Lines.Add(price);
                return basket.Lines.Count;
            });

        public static ScenarioAction<Basket> LinesAreAddedAndCounted(decimal first, decimal second) =>
            CreateActionStep($"Lines of {first} and {second} are added and counted", basket =>
            {
                basket.Lines.AddRange([first, second]);
                return basket.Lines.Count;
            });

        public static ScenarioAction<Basket> LinesAreAddedAndCountedLater(decimal first, decimal second, decimal third) =>
            CreateActionStep($"Lines of {first}, {second} and {third} are added and counted later", async basket =>
            {
                await Task.Yield();
                basket.Lines.AddRange([first, second, third]);
                return basket.Lines.Count;
            });

        public static ScenarioAction<Basket> TheLinesAreCounted { get; } = CreateActionStep("The lines are counted", basket => basket.Lines.Count);

        public static ScenarioExpectation<Basket> ItIs(decimal expected) =>
            CreateReturnedStep<decimal>($"It is {expected}", (_, total) => Assert.Equal(expected, total));

        public static ScenarioExpectation<Basket> ItIsPositive { get; } = CreateReturnedStep<decimal>("It is positive", (_, total, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.True(total > 0);
        });

        public static ScenarioExpectation<Basket> ThereAre(int count) =>
            CreateAssertionStep($"There are {count} lines", (basket, token) =>
            {
                token.ThrowIfCancellationRequested();
                Assert.Equal(count, basket.Lines.Count);
            });
    }
}
