using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class ExpectingExceptions
{
    [Fact]
    public Task AThrowsStepPassesTheExceptionToLaterChecks() =>
        Scenario.Define<ScenarioUnderTest>("A throws step passes its exception, typed, to the checks after it", Capabilities.Exceptions)
            .Given("A scenario that expects an ArgumentNullException and checks it twice", scenario => scenario.Use(Scenario.Define("A failure needs an error")
                .Given("No error", () => { })
                .WhenThrows<ArgumentNullException>("A failed result is created", () => ArgumentNullException.ThrowIfNull((object?)null, "error"))
                .Then("The rejection names 'error'", (ArgumentNullException rejection) => Assert.Equal("error", rejection.ParamName))
                .And("The rejection is seen again", (ArgumentNullException rejection) => scenario.Calls.Add(rejection.ParamName!))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("error"))
            .VerifyAsync();

    [Theory]
    [InlineData("a different exception", "Expected ArgumentNullException, but InvalidOperationException was thrown: The store is closed.")]
    [InlineData("a derived exception", "Expected ArgumentException, but ArgumentNullException was thrown: ")]
    [InlineData("no exception", "Expected ArgumentNullException, but nothing was thrown.")]
    public Task AnyOtherOutcomeFailsTheThrowsStepAndStopsTheScenario(string outcome, string explanation) =>
        Scenario.Define<ScenarioUnderTest>("Anything but the expected exception fails the throws step and stops the scenario", Capabilities.Exceptions,
                description: "A throws step expects exactly one exception type, as Assert.Throws does.")
            .Given("A scenario whose throws step meets " + outcome, scenario => scenario.Use(outcome == "a derived exception"
                ? Scenario.Define("A rejection")
                    .Given("Ready", () => { })
                    .WhenThrows<ArgumentException>("The call is made", () => ArgumentNullException.ThrowIfNull((object?)null, "error"))
                    .Then("It is checked", (ArgumentException _) => scenario.Calls.Add("then"))
                : Scenario.Define("A rejection")
                    .Given("Ready", () => { })
                    .WhenThrows<ArgumentNullException>("The call is made", () =>
                    {
                        if (outcome == "a different exception")
                        {
                            throw new InvalidOperationException("The store is closed.");
                        }
                    })
                    .Then("It is checked", (ArgumentNullException _) => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.StepIs(2, Unsatisfied))
            .And("The throws step says what it expected and what happened", scenario =>
                Assert.StartsWith("Assurance assertion failed: " + explanation, scenario.Step(2).Evaluation.Explanation))
            .And(RunSteps.StepIs(3, NotEvaluated))
            .And(RunSteps.TheStepsRanInTheOrder(""))
            .VerifyAsync();

    [Theory]
    [InlineData("an inline scenario")]
    [InlineData("a typed scenario")]
    [InlineData("a steps class")]
    public Task AThrowsAnyStepPassesForADerivedException(string form) =>
        Scenario.Define<ScenarioUnderTest>("A throws-any step passes for its exception type or one derived from it", Capabilities.Exceptions,
                description: "Work that is cancelled can throw OperationCanceledException or TaskCanceledException, as Assert.ThrowsAny accepts.")
            .Given($"A scenario in {form} that expects any OperationCanceledException and gets a TaskCanceledException", scenario => UseCancelledWork(form, scenario))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.TheStepsRanInTheOrder("TaskCanceledException"))
            .VerifyAsync();

    [Theory]
    [InlineData("an inline scenario")]
    [InlineData("a typed scenario")]
    [InlineData("a steps class")]
    public Task AThrowsStepCanBeAWholeScenario(string form) =>
        Scenario.Define<ScenarioUnderTest>("A throws step can be a whole scenario", Capabilities.Exceptions,
                description: "A call with nothing to set up starts at its When, and a throws step checks what its work threw, so it can end the scenario.")
            .Given($"A scenario in {form} of one throws step", scenario => UseOnlyAThrowsStep(form, scenario))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("Its one step is a When", scenario => Assert.Equal(StepKind.When, Assert.Single(scenario.Result.Steps).Kind))
            .VerifyAsync();

    [Fact]
    public Task AThrowsStepThatEndsAScenarioFailsItWhenNothingIsThrown() =>
        Scenario.Define<ScenarioUnderTest>("A throws step that ends a scenario fails it when nothing is thrown", Capabilities.Exceptions)
            .Given("A scenario whose only step expects an exception that isn't thrown", scenario => scenario.Use(Scenario.Define("An expected rejection")
                .WhenThrows<ArgumentNullException>("The call is made", () => { })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .And("The step says nothing was thrown", scenario =>
                Assert.StartsWith("Assurance assertion failed: Expected ArgumentNullException, but nothing was thrown.", scenario.Step(1).Evaluation.Explanation))
            .VerifyAsync();

    [Theory]
    [InlineData("a different exception", "Expected OperationCanceledException or a type derived from it, but InvalidOperationException was thrown: The store is closed.")]
    [InlineData("no exception", "Expected OperationCanceledException or a type derived from it, but nothing was thrown.")]
    public Task AThrowsAnyStepFailsForAnyOtherOutcome(string outcome, string explanation) =>
        Scenario.Define<ScenarioUnderTest>("A throws-any step fails for an unrelated exception or none", Capabilities.Exceptions)
            .Given("A scenario whose throws-any step meets " + outcome, scenario => scenario.Use(Scenario.Define("Work is cancelled")
                .Given("Ready", () => { })
                .WhenThrowsAny<OperationCanceledException>("The work runs", () =>
                {
                    if (outcome == "a different exception")
                    {
                        throw new InvalidOperationException("The store is closed.");
                    }
                })
                .Then("It is checked", (OperationCanceledException _) => scenario.Calls.Add("then"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.StepIs(2, Unsatisfied))
            .And("The throws step says what it expected and what happened", scenario =>
                Assert.StartsWith("Assurance assertion failed: " + explanation, scenario.Step(2).Evaluation.Explanation))
            .And(RunSteps.StepIs(3, NotEvaluated))
            .VerifyAsync();

    [Fact]
    public Task SeveralThrowsStepsKeepTheirOwnExceptions() =>
        Scenario.Define<ScenarioUnderTest>("Several throws steps keep their own exceptions", Capabilities.Exceptions)
            .Given("A scenario that expects two different exceptions and checks each", scenario => scenario.Use(Scenario.Define("Two lookups are rejected")
                .Given("An empty store", () => { })
                .WhenThrows<KeyNotFoundException>("A missing key is read", () => throw new KeyNotFoundException("missing"))
                .AndThrows<ArgumentNullException>("A null key is read", async () =>
                {
                    await Task.Yield();
                    ArgumentNullException.ThrowIfNull((object?)null, "key");
                })
                .Then("The missing key is named", (KeyNotFoundException rejection) => Assert.Equal("missing", rejection.Message))
                .And("The null key's parameter is named", (ArgumentNullException rejection) => Assert.Equal("key", rejection.ParamName))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ChecksOfOneTypePairInOrderWithItsThrowsSteps() =>
        Scenario.Define<ScenarioUnderTest>("Checks of one exception type pair, in order, with the throws steps of that type", Capabilities.Exceptions,
                description: "Two rejections of the same type are each checked, the first check reading the first rejection.")
            .Given("A scenario with two throws steps of one type and a check of each", scenario => scenario.Use(Scenario.Define("Two codes are rejected")
                .Given("A register of codes", () => { })
                .WhenThrows<ArgumentException>("An empty code is added", () => throw new ArgumentException("empty"))
                .AndThrows<ArgumentException>("A long code is added", () => throw new ArgumentException("too long"))
                .Then("The empty code is refused", (ArgumentException rejection) => scenario.Calls.Add(rejection.Message))
                .And("The long code is refused", (ArgumentException rejection) => scenario.Calls.Add(rejection.Message))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("empty, too long"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ACheckPairsWithTheThrowsStepOfItsExactTypeBeforeADerivedOne() =>
        Scenario.Define<ScenarioUnderTest>("A check pairs with the throws step of its exact type, before one of a derived type", Capabilities.Exceptions,
                description: "A missing argument and a contradictory one are refused with an exception and its base type, each checked.")
            .Given("A scenario that expects an ArgumentNullException and an ArgumentException, and checks each", scenario => scenario.Use(Scenario.Define("Two invalid states")
                .Given("A state factory", () => { })
                .WhenThrows<ArgumentNullException>("A state is created without a result", () => throw new ArgumentNullException("result"))
                .AndThrows<ArgumentException>("A state is created from a contradictory result", () => throw new ArgumentException("contradictory"))
                .Then("The missing result is named", (ArgumentNullException rejection) => scenario.Calls.Add(rejection.ParamName!))
                .And("The contradiction is named", (ArgumentException rejection) => scenario.Calls.Add(rejection.Message))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("result, contradictory"))
            .And(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task EachActionsChecksReadItsOwnThrowsStep() =>
        Scenario.Define<ScenarioUnderTest>("In a workflow, each action's checks read the exception of that action", Capabilities.Exceptions)
            .Given("A workflow whose two actions each throw the same type, each checked", scenario => scenario.Use(Scenario.Define("Two refusals in turn")
                .Given("A closed store", () => { })
                .WhenThrows<InvalidOperationException>("An order is placed", () => throw new InvalidOperationException("order"))
                .Then("The order is refused", (InvalidOperationException rejection) => scenario.Calls.Add(rejection.Message))
                .WhenThrows<InvalidOperationException>("A refund is asked for", () => throw new InvalidOperationException("refund"))
                .Then("The refund is refused", (InvalidOperationException rejection) => scenario.Calls.Add(rejection.Message))
                .And("It is still the refund", (InvalidOperationException rejection) => scenario.Calls.Add(rejection.Message))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheStepsRanInTheOrder("order, refund, refund"))
            .VerifyAsync();

    [Fact]
    public Task ChecksThatDontPairWithTheirThrowsStepsAreRefused() =>
        Scenario.Define<ScenarioUnderTest>("Checks of one type that don't pair with its throws steps are refused before anything runs", Capabilities.Exceptions,
                description: "With two throws steps of one type and a single check, it isn't clear which rejection the check means.")
            .Given("A scenario with two throws steps of one type and one check of it", scenario => scenario.Use(Scenario.Define("Two codes, one check")
                .Given("A register of codes", () => scenario.Calls.Add("ran"))
                .WhenThrows<ArgumentException>("An empty code is added", () => throw new ArgumentException("empty"))
                .AndThrows<ArgumentException>("A long code is added", () => throw new ArgumentException("too long"))
                .Then("A code is refused", (ArgumentException _) => { })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError(
                "pairs each check of ArgumentException with a step before it that throws or returns one, in order, but 2 steps throw or return one ('An empty code is added', "
                + "'A long code is added') and 1 check takes one ('A code is refused')"))
            .And(RunSteps.TheStepsRanInTheOrder(""))
            .VerifyAsync();

    [Fact]
    public Task ACheckOfAnExceptionNoStepExpectedStopsTheRun() =>
        Scenario.Define<ScenarioUnderTest>("A check of an exception that no step expected stops the run", Capabilities.Exceptions)
            .Given("A scenario that checks an ArgumentNullException without a throws step", scenario => scenario.Use(Scenario.Define("A check without a throws step")
                .Given("Ready", () => { })
                .When("Nothing throws", () => { })
                .Then("The rejection is checked", (ArgumentNullException _) => { })))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheRunStopsWithAnError("takes the ArgumentNullException that an earlier step throws or returns, but no earlier step throws or returns one"))
            .VerifyAsync();

    [Fact]
    public Task StepsClassesBuildThrowsSteps() =>
        Scenario.Define<ScenarioUnderTest>("Steps classes build throws steps and the checks of their exceptions", Capabilities.Exceptions)
            .Given("A typed scenario made of a steps class's throws step and check", scenario => scenario.Use(Scenario.Define<Tap>("Tap rejects a missing action, from a steps class")
                .Given(TapSteps.AStartingValue)
                .When(TapSteps.TapIsGivenNoAction)
                .Then(TapSteps.TheRejectionNames("action"))
                .And<ArgumentNullException>("The starting value is untouched", (tap, _) => Assert.Equal(42, tap.Value))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task TypedScenariosTakeThrowsStepsInline() =>
        Scenario.Define<ScenarioUnderTest>("Typed scenarios take throws steps inline", Capabilities.Exceptions)
            .Given("A typed scenario with an inline throws step and check", scenario => scenario.Use(Scenario.Define<Tap>("Tap rejects a missing action")
                .Given("A starting value", tap => tap.Value = 42)
                .WhenThrows<ArgumentNullException>("Tap is given no action", tap => tap.Apply(null!))
                .Then<ArgumentNullException>("It names 'action'", (_, rejection) => Assert.Equal("action", rejection.ParamName))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Theory]
    [InlineData("an inline scenario")]
    [InlineData("a typed scenario")]
    [InlineData("a steps class")]
    public Task AThrowsStepCanReadAValue(string form) =>
        Scenario.Define<ScenarioUnderTest>("A throws step's work can be a read that returns a value, as with Assert.Throws", Capabilities.Exceptions,
                description: "Reading a missing key or a fourth item of three is written as the read itself.")
            .Given($"A scenario in {form} whose throws step reads a missing key", scenario => UseReadingAMissingKey(form, scenario))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AStepMethodChecksAnExceptionWithAValue() =>
        Scenario.Define<ScenarioUnderTest>("A step method builds a check of an exception from a value", Capabilities.Exceptions)
            .Given("A typed scenario whose check of the exception takes the parameter name it expects", scenario => scenario.Use(
                Scenario.Define<Tap>("Tap rejects a missing action, checked with a value")
                    .Given(TapSteps.AStartingValue)
                    .When(TapSteps.TapIsGivenNoAction)
                    .Then(TapSteps.ItNames("action"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepIsNamed(3, "It names 'action'"))
            .VerifyAsync();

    [Theory]
    [InlineData(typeof(ArgumentNullException))]
    [InlineData(typeof(ArgumentOutOfRangeException))]
    public Task ATheoryRowCanNameTheExceptionItExpects(Type expected) =>
        Scenario.Define<ScenarioUnderTest>("A throws step can expect an exception type that each theory row names", Capabilities.Exceptions)
            .Given("A scenario whose throws step expects the row's exception, inline and in a steps class", scenario => scenario.Use(Scenario.Define<Tap>("A bad value is refused")
                .Given(TapSteps.AStartingValue)
                .When(TapSteps.ABadValueIsSet(expected))
                .AndThrows("It is set again", expected, tap => TapSteps.Refuse(expected))
                .Then("The first rejection names the value", (Tap _, ArgumentException rejection) => Assert.Equal("value", rejection.ParamName))
                .And("So does the second", (Tap _, ArgumentException rejection) => Assert.Equal("value", rejection.ParamName))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task AThrowsStepForATypeCanReturnAValue() =>
        Scenario.Define<ScenarioUnderTest>("A throws step for an exception type can run work that returns a value, such as a constructor", Capabilities.Exceptions)
            .Given("A scenario whose throws steps build a Uri from bad text, at once and later", scenario => scenario.Use(Scenario.Define("A bad address is refused")
                .Given("Some text that is not an address", () => { })
                .WhenThrows("A Uri is built from bad text", typeof(UriFormatException), () => new Uri("not an address"))
                .AndThrows("Another is built later", typeof(UriFormatException), async () =>
                {
                    await Task.Yield();
                    return (object?)new Uri("still not one");
                })
                .Then("The first is refused", (UriFormatException rejection) => Assert.NotNull(rejection))
                .And("So is the second", (UriFormatException rejection) => Assert.NotNull(rejection))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ATypeThatIsNotAnExceptionIsRefused() =>
        Scenario.Define<ScenarioUnderTest>("A throws step refuses a type that is not an exception", Capabilities.Exceptions)
            .Given("No scenario yet", _ => { })
            .WhenThrows<ArgumentException>("A throws step is written for a string", _ => Scenario.Define("A rejection").WhenThrows("It throws", typeof(string), () => { }))
            .Then("The refusal names the type", (ScenarioUnderTest _, ArgumentException rejection) =>
                Assert.StartsWith("A throws step expects an exception type; string is not one.", rejection.Message, StringComparison.Ordinal))
            .VerifyAsync();

    private static void UseReadingAMissingKey(string form, ScenarioUnderTest scenario)
    {
        if (form == "an inline scenario")
        {
            Dictionary<string, int> lookup = [];
            scenario.Use(Scenario.Define("A missing key is read")
                .Given("An empty lookup", () => { })
                .WhenThrows<KeyNotFoundException>("The key 'x' is read", () => lookup["x"])
                .Then("The key is missing", (KeyNotFoundException rejection) => Assert.Contains("'x'", rejection.Message, StringComparison.Ordinal)));
            return;
        }

        if (form == "a typed scenario")
        {
            scenario.Use(Scenario.Define<Tap>("A missing key is read")
                .Given("An empty lookup", _ => { })
                .WhenThrows<KeyNotFoundException>("The key 'x' is read", tap => tap.Lookup["x"])
                .Then(TapSteps.TheKeyIsMissing));
            return;
        }

        scenario.Use(Scenario.Define<Tap>("A missing key is read")
            .Given("An empty lookup", _ => { })
            .When(TapSteps.TheMissingKeyIsRead)
            .Then(TapSteps.TheKeyIsMissing));
    }

    private static void UseOnlyAThrowsStep(string form, ScenarioUnderTest scenario)
    {
        if (form == "an inline scenario")
        {
            Dictionary<string, int> lookup = [];
            scenario.Use(Scenario.Define("A missing key is read")
                .WhenThrows<KeyNotFoundException>("The key 'x' is read", () => lookup["x"]));
            return;
        }

        if (form == "a typed scenario")
        {
            scenario.Use(Scenario.Define<Tap>("A missing key is read")
                .WhenThrows<KeyNotFoundException>("The key 'x' is read", tap => tap.Lookup["x"]));
            return;
        }

        scenario.Use(Scenario.Define<Tap>("A missing key is read")
            .When(TapSteps.TheMissingKeyIsRead));
    }

    private static void UseCancelledWork(string form, ScenarioUnderTest scenario)
    {
        if (form == "an inline scenario")
        {
            scenario.Use(Scenario.Define("Work is cancelled")
                .Given("Ready", () => { })
                .WhenThrowsAny<OperationCanceledException>("The work runs", () => throw new TaskCanceledException())
                .Then("The cancellation is seen", (OperationCanceledException cancelled) => scenario.Calls.Add(cancelled.GetType().Name)));
            return;
        }

        if (form == "a typed scenario")
        {
            scenario.Use(Scenario.Define<Tap>("Work is cancelled")
                .Given("Ready", _ => { })
                .WhenThrowsAny<OperationCanceledException>("The work runs", async _ =>
                {
                    await Task.Yield();
                    throw new TaskCanceledException();
                })
                .Then<OperationCanceledException>("The cancellation is seen", (_, cancelled) => scenario.Calls.Add(cancelled.GetType().Name)));
            return;
        }

        scenario.Use(Scenario.Define<Tap>("Work is cancelled")
            .Given("Ready", _ => { })
            .When(TapSteps.TheWorkIsCancelled)
            .Then<OperationCanceledException>("The cancellation is seen", (_, cancelled) => scenario.Calls.Add(cancelled.GetType().Name)));
    }

    private sealed class Tap
    {
        public int Value { get; set; }

        public Dictionary<string, int> Lookup { get; } = [];

        public void Apply(Action<int> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action(Value);
        }
    }

    private sealed class TapSteps : ScenarioSteps<Tap>
    {
        public static ScenarioAction<Tap> AStartingValue { get; } = CreateActionStep("A starting value", tap => tap.Value = 42);

        public static ScenarioAction<Tap> TapIsGivenNoAction { get; } =
            CreateThrowsStep<ArgumentNullException>("Tap is given no action", tap => tap.Apply(null!));

        public static ScenarioAction<Tap> ABadValueIsSet(Type expected) =>
            CreateThrowsStep("A bad value is set", expected, _ => Refuse(expected));

        public static ScenarioAction<Tap> TheWorkIsCancelled { get; } =
            CreateThrowsAnyStep<OperationCanceledException>("The work runs", _ => throw new TaskCanceledException());

        public static ScenarioAction<Tap> TheMissingKeyIsRead { get; } =
            CreateThrowsStep<KeyNotFoundException>("The key 'x' is read", tap => tap.Lookup["x"]);

        public static ScenarioExpectation<Tap> TheKeyIsMissing { get; } =
            CreateThrownStep<KeyNotFoundException>("The key is missing", (_, rejection) => Assert.Contains("'x'", rejection.Message, StringComparison.Ordinal));

        public static ScenarioExpectation<Tap> ItNames(string parameter) =>
            CreateThrownStep<ArgumentNullException>($"It names '{parameter}'", (_, rejection) => Assert.Equal(parameter, rejection.ParamName));

        public static void Refuse(Type expected)
        {
            if (expected == typeof(ArgumentNullException))
            {
                throw new ArgumentNullException("value");
            }

            throw new ArgumentOutOfRangeException("value");
        }

        public static ScenarioExpectation<Tap> TheRejectionNames(string parameter) =>
            CreateThrownStep<ArgumentNullException>($"It names '{parameter}'", (_, rejection) => Assert.Equal(parameter, rejection.ParamName));
    }
}
