using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.MSTest.Tests;

/// <summary>Verifies scenarios through the MSTest adapter, and fails and skips tests the MSTest way.</summary>
/// <remarks>MSTest 3 has no current test context, so there the scenarios get the test's; on MSTest 4 they find the current test.</remarks>
public sealed class MSTestFramework(TestContext testContext) : IAdapterConformanceDriver
{
    /// <summary>What <c>TestContext.WriteLine</c> captured, read from MSTest's own context implementation.</summary>
    public string CapturedOutput => testContext.GetType().GetMethod("GetDiagnosticMessages")?.Invoke(testContext, null) as string ?? "";

    public Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
#if MSTEST3
        where TContext : notnull => scenario.VerifyWithCancellationAsync(options, cancellationToken, testContext);
#else
        where TContext : notnull => scenario.VerifyWithCancellationAsync(options, cancellationToken);
#endif

    public ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
        where TContext : notnull => ScenarioExpectation<TContext>.Assertion(name, assertion);

    public void FailAssertion(string message) => Assert.Fail(message);

    public bool IsNativeFailure(Exception exception) => exception is AssertFailedException;

    public void SkipTest(string reason) => Assert.Inconclusive(reason);

    public bool IsNativeSkip(Exception exception) => exception is AssertInconclusiveException;

    public static Scenario<AdapterUnderTest> Define(TestContext testContext, string name, BusinessContext business) =>
        Scenario.Define(name, () => new AdapterUnderTest(new MSTestFramework(testContext)), business);
}

public sealed class MSTestSteps : ScenarioSteps<AdapterUnderTest>
{
    public static ScenarioExpectation<AdapterUnderTest> TheRowIsRecorded(string outline, string row) =>
        CreateAssertionStep($"The scenario is recorded as an example of '{outline}', with {row}", adapter =>
        {
            ScenarioResult scenario = adapter.Evidence.Report.Run.Scenarios.Single();
            Assert.AreEqual(outline, scenario.OutlineId);
            Assert.AreEqual(row, string.Join(", ", scenario.Example.Select(value => $"{value.Name}: {value.Value}")));
            Assert.AreEqual($"{outline} [{row}]", scenario.Id);
        });

    public static ScenarioExpectation<AdapterUnderTest> TheEvidenceNamesTheTest(string test) =>
        CreateAssertionStep($"The evidence names the test '{test}'", adapter =>
            Assert.AreEqual($"{typeof(MSTestAdapterTests).FullName}.{test}", adapter.Evidence.TestName));
}
