using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.MSTest;

/// <summary>Explicit multi-scenario verification entry point.</summary>
public static class AssuranceAssert
{
#if !MSTEST3
    /// <summary>Executes and verifies the scenarios. They are cancelled with the current MSTest test, including by its timeout.
    /// Needs MSTest 4.5 or later; on earlier versions, pass the test's context.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios)
        where TContext : notnull
        => AssuranceVerification.VerifyAsync(
            name, scenarios, AssuranceMSTestConfiguration.DefaultReportOptions, testContext: null);

    /// <summary>Executes and verifies the scenarios using the supplied report configuration. Needs MSTest 4.5 or later; on earlier
    /// versions, pass the test's context.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios,
        ReportOptions reportOptions)
        where TContext : notnull
        => AssuranceVerification.VerifyAsync(name, scenarios, reportOptions, testContext: null);
#endif

    /// <summary>Executes and verifies the scenarios for the test that <paramref name="testContext"/> belongs to. They are cancelled with
    /// that test, including by its timeout.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios, TestContext testContext)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(testContext);
        return AssuranceVerification.VerifyAsync(name, scenarios, AssuranceMSTestConfiguration.DefaultReportOptions, testContext);
    }

    /// <summary>Executes and verifies the scenarios using the supplied report configuration, for the test that
    /// <paramref name="testContext"/> belongs to.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="testContext">The running test's context: the test class's <c>TestContext</c> property.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios, TestContext testContext,
        ReportOptions reportOptions) where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(testContext);
        return AssuranceVerification.VerifyAsync(name, scenarios, reportOptions, testContext);
    }
}
