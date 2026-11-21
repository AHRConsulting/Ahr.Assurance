using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Xunit;

/// <summary>Explicit multi-scenario verification entry point.</summary>
public static class AssuranceAssert
{
    /// <summary>Executes and verifies the scenarios. They are cancelled with the current xUnit test, including by its timeout.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios)
        where TContext : notnull
        => AssuranceVerification.VerifyAsync(
            name, scenarios, AssuranceXunitConfiguration.DefaultReportOptions);

    /// <summary>Executes and verifies the scenarios using the supplied report configuration.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios,
        ReportOptions reportOptions)
        where TContext : notnull
        => AssuranceVerification.VerifyAsync(name, scenarios, reportOptions);

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios,
        ReportOptions reportOptions, CancellationToken cancellationToken) where TContext : notnull =>
        AssuranceVerification.VerifyAsync(name, scenarios, reportOptions, cancellationToken);

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios,
        CancellationToken cancellationToken) where TContext : notnull =>
        AssuranceVerification.VerifyAsync(name, scenarios, AssuranceXunitConfiguration.DefaultReportOptions, cancellationToken);
}
