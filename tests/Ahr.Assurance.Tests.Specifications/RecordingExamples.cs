using System.Collections;
using System.Globalization;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class RecordingExamples
{
    public static TheoryData<string, int, bool> Amounts => new()
    {
        { "GBP", 100, true },
        { "GBP", -1, false },
    };

    public static TheoryData<string[]> Frameworks => [new(["xunit-v3", "playwright"]), new(["mstest"])];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task InlineDataRowsAreRecorded(string? message) =>
        Scenario.Define<ScenarioUnderTest>("Each InlineData row is recorded as an example of its scenario", Capabilities.Examples)
            .Given("A scenario in a test that has a row for each message", scenario => scenario.Use(AScenario("An error needs a readable message")))
            .When(RunSteps.ItIsVerified)
            .Then("The scenario is an example of its outline", scenario =>
                Assert.Equal("An error needs a readable message", scenario.Sink.Scenario.OutlineId))
            .And("The row's value is recorded, with null written as null", scenario =>
                Assert.Equal($"message = {message ?? "null"}", RunSteps.Values(scenario.Sink.Scenario.Example)))
            .And("The scenario's ID includes the row", scenario =>
                Assert.Equal($"An error needs a readable message [message: {message ?? "null"}]", scenario.Sink.Scenario.Id))
            .VerifyAsync();

    [Theory]
    [MemberData(nameof(Amounts))]
    public Task MemberDataRowsAreRecorded(string currency, int pence, bool accepted) =>
        Scenario.Define<ScenarioUnderTest>("Each MemberData row is recorded, in parameter order", Capabilities.Examples)
            .Given("A scenario in a test that has a row for each amount", scenario => scenario.Use(AScenario("A charge is accepted only for a valid amount")))
            .When(RunSteps.ItIsVerified)
            .Then("The row's values are recorded by parameter name", scenario => Assert.Equal(
                $"currency = {currency}, pence = {pence.ToString(CultureInfo.InvariantCulture)}, accepted = {(accepted ? "true" : "false")}",
                RunSteps.Values(scenario.Sink.Scenario.Example)))
            .VerifyAsync();

    [Theory]
    [MemberData(nameof(Frameworks))]
    public Task ArrayRowsAreRecordedByTheirItems(string[] frameworks) =>
        Scenario.Define<ScenarioUnderTest>("Each row with an array is recorded by its items", Capabilities.Examples)
            .Given("A scenario in a test that has a row for each list of frameworks", scenario => scenario.Use(AScenario("A project gets an adapter for each framework")))
            .When(RunSteps.ItIsVerified)
            .Then("The row's items are recorded", scenario =>
                Assert.Equal($"frameworks = [{string.Join(", ", frameworks)}]", RunSteps.Values(scenario.Sink.Scenario.Example)))
            .And("Each row has its own scenario ID", scenario =>
                Assert.Equal($"A project gets an adapter for each framework [frameworks: [{string.Join(", ", frameworks)}]]", scenario.Sink.Scenario.Id))
            .VerifyAsync();

    [Theory]
    [ClassData(typeof(Decimals))]
    public Task ClassDataRowsAreRecorded(decimal amount) =>
        Scenario.Define<ScenarioUnderTest>("Each ClassData row is recorded exactly", Capabilities.Examples)
            .Given("A scenario in a test that has a row for each decimal", scenario => scenario.Use(AScenario("Amounts are recorded exactly")))
            .When(RunSteps.ItIsVerified)
            .Then("The decimal is recorded without rounding", scenario =>
                Assert.Equal(amount.ToString(CultureInfo.InvariantCulture), Assert.Single(scenario.Sink.Scenario.Example).Value))
            .VerifyAsync();

    [Fact]
    public Task AFactHasNoExample() =>
        Scenario.Define<ScenarioUnderTest>("A test without rows records no example", Capabilities.Examples)
            .Given("A scenario in a plain test", scenario => scenario.Use(AScenario("A plain test")))
            .When(RunSteps.ItIsVerified)
            .Then("It has no outline and no example, and its ID is its name", scenario =>
            {
                Assert.Null(scenario.Sink.Scenario.OutlineId);
                Assert.Empty(scenario.Sink.Scenario.Example);
                Assert.Equal("A plain test", scenario.Sink.Scenario.Id);
            })
            .VerifyAsync();

    private static Scenario AScenario(string name) => Scenario.Define(name)
        .Given("A value", () => { })
        .When("It is used", () => { })
        .Then("It is recorded", () => { });

    private sealed class Decimals : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return [1.5m];
            yield return [0.1m];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
