using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.TUnit;

/// <summary>Executes and verifies scenarios in TUnit tests.</summary>
public static class ScenarioVerificationExtensions
{
    /// <summary>Executes and verifies this scenario. It is cancelled with the current TUnit test, including by its timeout.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        this Scenario<TContext> scenario)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario]);
    }

    /// <summary>Executes and verifies this scenario using the supplied report configuration.</summary>
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

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(this Scenario<TContext> scenario, CancellationToken cancellationToken)
        where TContext : notnull =>
        AssuranceAssert.VerifyWithCancellationAsync(scenario.Name, [scenario], AssuranceTUnitConfiguration.DefaultReportOptions, cancellationToken);

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(this Scenario<TContext> scenario, ReportOptions reportOptions,
        CancellationToken cancellationToken) where TContext : notnull =>
        AssuranceAssert.VerifyWithCancellationAsync(scenario.Name, [scenario], reportOptions, cancellationToken);

}
