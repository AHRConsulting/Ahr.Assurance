using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.MSTest;

/// <summary>Verification for local-variable scenarios, using the standard verification lifecycle.</summary>
public static class InlineScenarioExtensions
{
#if !MSTEST3
    /// <summary>Executes, reports, and asserts. The scenario is cancelled with the current MSTest test, including by its timeout.
    /// Needs MSTest 4.5 or later; on earlier versions, pass the test's context with <see cref="VerifyAsync(Scenario, TestContext)"/>.</summary>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync();
    }

    /// <summary>Verifies with explicit outputs. The scenario is cancelled with the current MSTest test, including by its timeout.
    /// Needs MSTest 4.5 or later; on earlier versions, pass the test's context with <see cref="VerifyAsync(Scenario, TestContext, ReportOptions)"/>.</summary>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, ReportOptions reportOptions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(reportOptions);
    }
#endif

    /// <summary>Executes, reports, and asserts for the test that <paramref name="testContext"/> belongs to. The scenario is cancelled with
    /// that test, including by its timeout.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(testContext);
    }

    /// <summary>Verifies with explicit outputs, for the test that <paramref name="testContext"/> belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, TestContext testContext, ReportOptions reportOptions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(testContext, reportOptions);
    }

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync(this Scenario scenario, ReportOptions reportOptions,
        CancellationToken cancellationToken, TestContext? testContext = null) =>
        scenario.Definition.VerifyWithCancellationAsync(reportOptions, cancellationToken, testContext);
}
