using System.Collections.Concurrent;
using System.Globalization;
using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Xunit.Sdk;
using static System.FormattableString;
using static Ahr.Assurance.Adapters.Conformance.AdapterSteps;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.XunitV2.Tests;

public sealed class XunitV2AdapterTests(ITestOutputHelper output)
{
    public static TheoryData<string> Cases => [.. AdapterConformance.CaseNames];

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Conforms(string conformanceCase) => AdapterConformance.RunAsync(conformanceCase, new XunitV2Driver(output));

    [Fact]
    public Task AFailedAssertionFailsTheTestTheXunitWay() =>
        Define("A failed xUnit assertion fails the test the xUnit way, naming xUnit", AdapterBehaviour.Verdicts)
            .Given("A scenario whose check expects 1 and finds 2", adapter => adapter.Use(adapter.Simple(_ => Assert.Equal(1, Total(2)), "A failing check")))
            .When(ItIsVerified)
            .Then(TheTestFailsNatively)
            .And(TheFailureSays("xUnit assertion failed"))
            .And(TheVerdictIs(Unsatisfied))
            .VerifyAsync(output);

    [Fact]
    public Task EvidenceNamesTheXunitTest() =>
        Define("Evidence names the xUnit test it came from", AdapterBehaviour.Identity)
            .Given("A passing scenario", adapter => adapter.Use(adapter.Simple(_ => { }, "Identity")))
            .When(ItIsVerified)
            .Then("The evidence has an ID and the test's name", adapter =>
            {
                Assert.Equal($"{typeof(XunitV2AdapterTests).FullName}.{nameof(EvidenceNamesTheXunitTest)}", adapter.Evidence.TestName);
                Assert.False(string.IsNullOrEmpty(adapter.Evidence.TestId));
            })
            .And("The evidence's producer is the test assembly", adapter =>
                Assert.Equal(typeof(XunitV2AdapterTests).Assembly.GetName().Name, adapter.Evidence.Producer))
            .VerifyAsync(output);

    [Theory]
    [InlineData("GBP", 100)]
    [InlineData("EUR", -1)]
    public Task TheoryRowsAreExamples(string currency, int pence) =>
        Define("Each theory row is recorded as an example of its scenario", AdapterBehaviour.Identity)
            .Given("A scenario in a test that has a row for each charge", adapter =>
                adapter.Use(adapter.Simple(_ => Assert.False(string.IsNullOrEmpty(currency)), "A charge is recorded")))
            .When(ItIsVerified)
            .Then("The row's values are recorded by parameter name", adapter => Assert.Equal(
                Invariant($"currency: {currency}, pence: {pence}"),
                string.Join(", ", adapter.Evidence.Report.Run.Scenarios.Single().Example.Select(value => $"{value.Name}: {value.Value}"))))
            .And("The scenario's ID includes the row", adapter => Assert.Equal(
                Invariant($"A charge is recorded [currency: {currency}, pence: {pence}]"), adapter.Evidence.Report.Run.Scenarios.Single().Id))
            .VerifyAsync(output);

    [Fact]
    public Task AnOutputHelperOfAnotherKindStillGetsTheOutput()
    {
        LinesOutput lines = new();
        return Define("An output helper that isn't xUnit's own still gets the scenario's text", AdapterBehaviour.Identity)
            .Given("A passing scenario, with compact text output", adapter => adapter.TextOutput = AssuranceTextVerbosity.Compact)
            .When("It is verified with an output helper of another kind", async adapter => _ = await Scenario.Define("Output to another helper", () => new object())
                .Given("Ready", _ => { })
                .When("Act", _ => { })
                .Then("Nothing fails", _ => { })
                .VerifyAsync(lines, adapter.Options))
            .Then("The helper got a line naming the scenario", _ =>
                Assert.Contains(lines.Lines, line => line.Contains("Output to another helper", StringComparison.Ordinal)))
            .And("The evidence names no test, since the helper can't say which", adapter => Assert.Null(adapter.Evidence.TestName))
            .VerifyAsync(output);
    }

    private Scenario<AdapterUnderTest> Define(string name, BusinessContext business) =>
        Scenario.Define(name, () => new AdapterUnderTest(new XunitV2Driver(output)), business);

    private static int Total(int value) => value;

    private sealed class XunitV2Driver(ITestOutputHelper output) : IAdapterConformanceDriver
    {
        public string CapturedOutput => (output as TestOutputHelper)?.Output ?? "";

        public Task<AssuranceVerificationResult> VerifyAsync<TContext>(
            Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
            where TContext : notnull => scenario.VerifyWithCancellationAsync(output, options, cancellationToken);

        public ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
            where TContext : notnull => ScenarioExpectation<TContext>.Assertion(name, assertion);

        public void FailAssertion(string message) => Assert.Fail(message);

        public bool IsNativeFailure(Exception exception) => exception is XunitException;

        public void SkipTest(string reason) => Skip.If(true, reason);

        public bool IsNativeSkip(Exception exception) => exception is global::Xunit.SkipException;
    }

    private sealed class LinesOutput : ITestOutputHelper
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public void WriteLine(string message) => Lines.Enqueue(message);
        public void WriteLine(string format, params object[] args) => Lines.Enqueue(string.Format(CultureInfo.InvariantCulture, format, args));
    }
}
