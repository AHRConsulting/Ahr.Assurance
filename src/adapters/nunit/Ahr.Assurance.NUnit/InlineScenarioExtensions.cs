using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.NUnit;

/// <summary>Verification for local-variable scenarios, using the standard verification lifecycle.</summary>
public static class InlineScenarioExtensions
{
    /// <summary>Executes, reports, and asserts. The scenario is cancelled with the current NUnit test, including by its timeout.</summary>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync();
    }

    /// <summary>Verifies with explicit outputs. The scenario is cancelled with the current NUnit test, including by its timeout.</summary>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, ReportOptions reportOptions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(reportOptions);
    }

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync(this Scenario scenario, CancellationToken cancellationToken) =>
        scenario.Definition.VerifyWithCancellationAsync(cancellationToken);

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync(this Scenario scenario, ReportOptions reportOptions, CancellationToken cancellationToken) =>
        scenario.Definition.VerifyWithCancellationAsync(reportOptions, cancellationToken);
}
