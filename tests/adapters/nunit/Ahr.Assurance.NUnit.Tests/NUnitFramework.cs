using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using NUnit.Framework.Internal;

namespace Ahr.Assurance.NUnit.Tests;

/// <summary>Verifies scenarios through the NUnit adapter, and fails and skips tests the NUnit way.</summary>
public sealed class NUnitFramework : IAdapterConformanceDriver
{
    public string CapturedOutput => TestExecutionContext.CurrentContext.CurrentResult.Output;

    public Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
        where TContext : notnull => scenario.VerifyWithCancellationAsync(options, cancellationToken);

    public ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
        where TContext : notnull => ScenarioExpectation<TContext>.Assertion(name, assertion);

    public void FailAssertion(string message) => Assert.Fail(message);

    public bool IsNativeFailure(Exception exception) => exception is AssertionException;

    public void SkipTest(string reason) => Assert.Ignore(reason);

    public bool IsNativeSkip(Exception exception) => exception is IgnoreException;

    public static Scenario<AdapterUnderTest> Define(string name, BusinessContext business, string? description = null) =>
        Scenario.Define(name, () => new AdapterUnderTest(new NUnitFramework()), business, description: description);
}

public sealed class NUnitSteps : ScenarioSteps<AdapterUnderTest>
{
    public static ScenarioExpectation<AdapterUnderTest> TheRowIsRecorded(string outline, string row) =>
        CreateAssertionStep($"The scenario is recorded as an example of '{outline}', with {row}", adapter =>
        {
            ScenarioResult scenario = adapter.Evidence.Report.Run.Scenarios.Single();
            Assert.That(scenario.OutlineId, Is.EqualTo(outline));
            Assert.That(string.Join(", ", scenario.Example.Select(value => $"{value.Name}: {value.Value}")), Is.EqualTo(row));
            Assert.That(scenario.Id, Is.EqualTo($"{outline} [{row}]"));
        });
}

/// <summary>What the NUnit adapter promises on top of what every adapter does.</summary>
public static class NUnitBehaviour
{
    public static BusinessContext Steps { get; } = BusinessContext.Define("nunit-steps", "Steps under NUnit",
            "NUnit records a failed assertion on the test that made it. Each step runs in its own NUnit context, so a failure the test handles stays handled, while the scenario's state and output still reach the test.")
        .Capability("step-isolation", "Step isolation")
        .Outcome("handled-stays-handled", "A test fails only for failures it doesn't handle")
        .Build();
}
