using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.MSTest;

/// <summary>Executes and verifies scenarios in MSTest tests.</summary>
public static class ScenarioVerificationExtensions
{
#if !MSTEST3
    /// <summary>Executes and verifies this scenario. It is cancelled with the current MSTest test, including by its timeout.
    /// Needs MSTest 4.5 or later; on earlier versions, pass the test's context.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        this Scenario<TContext> scenario)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario]);
    }

    /// <summary>Executes and verifies this scenario using the supplied report configuration. Needs MSTest 4.5 or later; on earlier
    /// versions, pass the test's context.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        this Scenario<TContext> scenario,
        ReportOptions reportOptions)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario], reportOptions);
    }
#endif

    /// <summary>Executes and verifies this scenario for the test that <paramref name="testContext"/> belongs to. It is cancelled with that
    /// test, including by its timeout.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(this Scenario<TContext> scenario, TestContext testContext)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario], testContext);
    }

    /// <summary>Executes and verifies this scenario using the supplied report configuration, for the test that
    /// <paramref name="testContext"/> belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(this Scenario<TContext> scenario, TestContext testContext,
        ReportOptions reportOptions) where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario], testContext, reportOptions);
    }

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(this Scenario<TContext> scenario, ReportOptions reportOptions,
        CancellationToken cancellationToken, TestContext? testContext = null) where TContext : notnull =>
        AssuranceVerification.VerifyAsync(scenario.Name, [scenario], reportOptions, testContext, cancellationToken);
}
