using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using TUnit.Assertions.Exceptions;
using TUnit.Core.Exceptions;

namespace Ahr.Assurance.TUnit.Tests;

/// <summary>Verifies scenarios through the TUnit adapter, and fails and skips tests the TUnit way.</summary>
public sealed class TUnitFramework : IAdapterConformanceDriver
{
    public string CapturedOutput => TestContext.Current?.Output.GetStandardOutput() ?? "";

    public Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
        where TContext : notnull => scenario.VerifyWithCancellationAsync(options, cancellationToken);

    public ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
        where TContext : notnull => ScenarioExpectation<TContext>.Assertion(name, assertion);

    public void FailAssertion(string message) => Assert.Fail(message);

    public bool IsNativeFailure(Exception exception) => exception is AssertionException;

    public void SkipTest(string reason) => Skip.Test(reason);

    public bool IsNativeSkip(Exception exception) => exception is SkipTestException;

    public static Scenario<AdapterUnderTest> Define(string name, BusinessContext business) =>
        Scenario.Define(name, () => new AdapterUnderTest(new TUnitFramework()), business);
}

public sealed class TUnitSteps : ScenarioSteps<AdapterUnderTest>
{
    public static ScenarioExpectation<AdapterUnderTest> TheRowIsRecorded(string outline, string row) =>
        CreateAssertionStep($"The scenario is recorded as an example of '{outline}', with {row}", async adapter =>
        {
            ScenarioResult scenario = adapter.Evidence.Report.Run.Scenarios.Single();
            await Assert.That(scenario.OutlineId).IsEqualTo(outline);
            await Assert.That(string.Join(", ", scenario.Example.Select(value => $"{value.Name}: {value.Value}"))).IsEqualTo(row);
            await Assert.That(scenario.Id).IsEqualTo($"{outline} [{row}]");
        });
}
