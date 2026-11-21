using System.Globalization;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class ReusingSteps
{
    [Fact]
    public Task StepsForAnInterfaceServeEveryContextThatImplementsIt() =>
        Scenario.Define<ScenarioUnderTest>("Steps written for an interface serve every context that implements it", Capabilities.Reuse)
            .Given("A basket scenario and an order scenario that both check a rejection with the same steps", scenario => scenario.Use(
                Scenario.Define<Basket>("A basket rejects a missing item")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.AMissingItemIsAdded)
                    .Then(RejectionSteps.TheCallIsRejectedForAMissingArgument)
                    .And(RejectionSteps.TheRejectionNames("item"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("The same steps check an order", async (scenario, token) =>
            {
                scenario.Use(Scenario.Define<Order>("An order rejects a missing customer")
                    .Given(OrderSteps.ANewOrder)
                    .When(OrderSteps.AMissingCustomerIsSet)
                    .Then(RejectionSteps.TheCallIsRejectedForAMissingArgument)
                    .And(RejectionSteps.TheRejectionNames("customer")));
                scenario.Outcome = await scenario.RunAsync(token);
                Assert.Equal(Satisfied, scenario.Run.Status);
            })
            .And(RunSteps.StepIsNamed(4, "The rejection names the 'customer' parameter"))
            .VerifyAsync();

    [Fact]
    public Task OneContextCombinesStepsFromUnrelatedAreas() =>
        Scenario.Define<ScenarioUnderTest>("One context combines steps from unrelated areas", Capabilities.Reuse)
            .Given("A reader scenario that uses network, storage and reader steps", scenario => scenario.Use(
                Scenario.Define<OfflineReaderContext>("An offline reader opens a saved document")
                    .Given(NetworkSteps.TheDeviceIsOffline)
                    .And(StorageSteps.ADocumentIsSaved("handbook"))
                    .When(OfflineReaderSteps.TheReaderOpens("handbook"))
                    .Then(OfflineReaderSteps.TheSavedCopyIsReturnedWithoutTheNetwork)
                    .And(NetworkSteps.TheNetworkWasNotContacted)))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    [Fact]
    public Task ASharedCheckFailsWhenItsContextDisagrees() =>
        Scenario.Define<ScenarioUnderTest>("A shared check fails when its context disagrees", Capabilities.Reuse)
            .Given("A basket scenario that expects the wrong parameter to be named", scenario => scenario.Use(
                Scenario.Define<Basket>("A basket names the wrong parameter")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.AMissingItemIsAdded)
                    .Then(RejectionSteps.TheRejectionNames("customer"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Unsatisfied))
            .And("The check's failure is the assertion library's", scenario =>
                Assert.StartsWith("xUnit assertion failed", scenario.Step(3).Evaluation.Explanation))
            .VerifyAsync();

    [Fact]
    public Task TracedValuesAreRecordedTheSameInEveryCulture() =>
        Scenario.Define<ScenarioUnderTest>("Values a step traces are recorded under their names, the same in every culture", Capabilities.Reuse)
            .Given("A basket scenario that charges 1.5 GBP and traces its values", _ => { })
            .When("It is built and run in a German culture", async (scenario, token) =>
            {
                CultureInfo original = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                try
                {
                    scenario.Use(Scenario.Define<Basket>("A charge is recorded")
                        .Given(BasketSteps.AnEmptyBasket)
                        .When(BasketSteps.ACharge("GBP", 1.5m))
                        .Then(BasketSteps.TheTotalIs(1.5m, null))
                        .And(BasketSteps.TheChargeIsAccepted(true)));
                    scenario.Outcome = await scenario.RunAsync(token);
                }
                finally
                {
                    CultureInfo.CurrentCulture = original;
                }
            })
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepRecords(2, "currency = GBP, amount = 1.5"))
            .And(RunSteps.StepRecords(3, "total = 1.5, note = null"))
            .And(RunSteps.StepRecords(4, "expected acceptance = True"))
            .And(RunSteps.StepRecords(1, ""))
            .VerifyAsync();

    [Fact]
    public Task AStepCanTraceAValueItsNameLeavesOut() =>
        Scenario.Define<ScenarioUnderTest>("A step can trace a value that its name leaves out", Capabilities.Reuse)
            .Given("A basket scenario that applies a promotion code", scenario => scenario.Use(
                Scenario.Define<Basket>("A promotion is applied")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.APromotionCode("SAVE3"))
                    .Then(BasketSteps.TheTotalIs(-5m, null))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepIsNamed(2, "A promotion code is applied"))
            .And(RunSteps.StepRecords(2, "code = SAVE3"))
            .VerifyAsync();

    [Fact]
    public Task ATracedCollectionIsRecordedAsItsItems() =>
        Scenario.Define<ScenarioUnderTest>("A traced collection is recorded as its items", Capabilities.Reuse)
            .Given("A basket scenario that applies two promotion codes and traces a list of amounts", scenario => scenario.Use(
                Scenario.Define<Basket>("Promotions are applied")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.PromotionCodes(["SAVE3", "SAVE5"], [1.5m, null]))
                    .Then(BasketSteps.TheTotalIs(-2m, null))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepRecords(2, "codes = [SAVE3, SAVE5], amounts = [1.5, null]"))
            .VerifyAsync();

    [Fact]
    public Task AStepThatTracesNothingRecordsNothing() =>
        Scenario.Define<ScenarioUnderTest>("A step method whose name shows its value records nothing else", Capabilities.Reuse)
            .Given("A basket scenario with an async refund step and an async check", scenario => scenario.Use(
                Scenario.Define<Basket>("A refund is made")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.ARefund(2m))
                    .Then(BasketSteps.TheTotalSettlesAt(-2m))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And(RunSteps.StepIsNamed(2, "A refund of 2 is made"))
            .And(RunSteps.StepRecords(2, ""))
            .VerifyAsync();

    [Fact]
    public Task DefineCreatesAFreshContextForEachRun() =>
        Scenario.Define<ScenarioUnderTest>("A context made by Define is fresh for each run", Capabilities.Reuse)
            .Given("A basket scenario that charges 2 and expects a total of 2", scenario => scenario.Use(
                Scenario.Define<Basket>("Each run starts empty")
                    .Given(BasketSteps.AnEmptyBasket)
                    .When(BasketSteps.ACharge("GBP", 2m))
                    .Then(BasketSteps.TheTotalIs(2m, "fresh"))))
            .When(RunSteps.ItIsRun)
            .And(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .And("Its ID is its name", scenario => Assert.Equal("Each run starts empty", scenario.Result.Id))
            .VerifyAsync();

    private interface INetwork
    {
        FakeConnectivity Connectivity { get; }
    }

    private interface IOfflineStorage
    {
        Dictionary<string, string> Saved { get; }
    }

    private interface IOfflineReader : INetwork, IOfflineStorage
    {
        string? Opened { get; set; }
    }

    private sealed class FakeConnectivity
    {
        public bool Online { get; set; } = true;
        public int Calls { get; set; }
    }

    private sealed class OfflineReaderContext : IOfflineReader
    {
        public FakeConnectivity Connectivity { get; } = new();
        public Dictionary<string, string> Saved { get; } = [];
        public string? Opened { get; set; }
    }

    private sealed class NetworkSteps : ScenarioSteps<INetwork>
    {
        public static ScenarioAction<INetwork> TheDeviceIsOffline { get; } = CreateActionStep("The device is offline", context => context.Connectivity.Online = false);

        public static ScenarioExpectation<INetwork> TheNetworkWasNotContacted { get; } =
            CreateAssertionStep("The network was not contacted", context => Assert.Equal(0, context.Connectivity.Calls));
    }

    private sealed class StorageSteps : ScenarioSteps<IOfflineStorage>
    {
        public static ScenarioAction<IOfflineStorage> ADocumentIsSaved(string document) =>
            CreateActionStep($"The '{document}' document is saved", context => context.Saved[document] = "saved copy");
    }

    private sealed class OfflineReaderSteps : ScenarioSteps<IOfflineReader>
    {
        public static ScenarioAction<IOfflineReader> TheReaderOpens(string document) =>
            CreateActionStep($"The reader opens '{document}'", context =>
            {
                if (context.Connectivity.Online)
                {
                    context.Connectivity.Calls++;
                }

                context.Opened = context.Saved.GetValueOrDefault(document);
            });

        public static ScenarioExpectation<IOfflineReader> TheSavedCopyIsReturnedWithoutTheNetwork { get; } =
            CreateAssertionStep("The saved copy is returned", context => Assert.Equal("saved copy", context.Opened));
    }

    private interface IRecordsThrown
    {
        Exception? Thrown { get; set; }
    }

    private sealed class Basket : IRecordsThrown
    {
        public Exception? Thrown { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class Order : IRecordsThrown
    {
        public Exception? Thrown { get; set; }
        public string? Customer { get; set; }
    }

    private sealed class RejectionSteps : ScenarioSteps<IRecordsThrown>
    {
        public static ScenarioExpectation<IRecordsThrown> TheCallIsRejectedForAMissingArgument { get; } =
            CreateAssertionStep("The call is rejected because an argument is missing", context => Assert.IsType<ArgumentNullException>(context.Thrown));

        public static ScenarioExpectation<IRecordsThrown> TheRejectionNames(string parameter) =>
            CreateAssertionStep($"The rejection names the '{parameter}' parameter",
                context => Assert.Equal(parameter, Assert.IsType<ArgumentNullException>(context.Thrown).ParamName));
    }

    private sealed class BasketSteps : ScenarioSteps<Basket>
    {
        public static ScenarioAction<Basket> AnEmptyBasket { get; } = CreateActionStep("An empty basket", basket => basket.Total = 0);

        public static ScenarioAction<Basket> AMissingItemIsAdded { get; } =
            CreateActionStep("A missing item is added", basket => basket.Thrown = Record.Exception(() => ArgumentNullException.ThrowIfNull((object?)null, "item")));

        public static ScenarioAction<Basket> ACharge(string currency, decimal amount) =>
            CreateActionStep($"A charge of {amount} {currency} is made", async (basket, token) =>
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                basket.Total += amount;
            }).WithValue(currency).WithValue(amount);

        public static ScenarioExpectation<Basket> TheTotalIs(decimal total, string? note) =>
            CreateAssertionStep($"The total is {total}, noted as {note}", basket => Assert.Equal(total, basket.Total))
                .WithValue(total).WithValue(note);

        public static ScenarioExpectation<Basket> TheChargeIsAccepted(bool accepted) =>
            CreateAssertionStep($"The charge is accepted: {accepted}", basket => Assert.Equal(accepted, basket.Total > 0))
                .WithValue(accepted, "expected acceptance");

        public static ScenarioAction<Basket> APromotionCode(string code) =>
            CreateActionStep("A promotion code is applied", basket => basket.Total -= code.Length).WithValue(code);

        public static ScenarioAction<Basket> PromotionCodes(string[] codes, List<decimal?> amounts) =>
            CreateActionStep("Promotion codes are applied", basket => basket.Total -= codes.Length).WithValue(codes).WithValue(amounts);

        public static ScenarioAction<Basket> ARefund(decimal amount) =>
            CreateActionStep($"A refund of {amount} is made", async basket =>
            {
                await Task.Yield();
                basket.Total -= amount;
            });

        public static ScenarioExpectation<Basket> TheTotalSettlesAt(decimal total) =>
            CreateAssertionStep($"The total settles at {total}", async basket =>
            {
                await Task.Yield();
                Assert.Equal(total, basket.Total);
            });
    }

    private sealed class OrderSteps : ScenarioSteps<Order>
    {
        public static ScenarioAction<Order> ANewOrder { get; } = CreateActionStep("A new order", order => order.Customer = "placeholder");

        public static ScenarioAction<Order> AMissingCustomerIsSet { get; } =
            CreateActionStep("A missing customer is set", order => order.Thrown = Record.Exception(() => ArgumentNullException.ThrowIfNull((object?)null, "customer")));
    }
}
