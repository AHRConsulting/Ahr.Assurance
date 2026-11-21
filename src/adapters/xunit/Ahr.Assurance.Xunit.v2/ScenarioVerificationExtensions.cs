using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Xunit.Abstractions;

namespace Ahr.Assurance.Xunit;

/// <summary>Executes and verifies scenarios in xUnit v2 tests.</summary>
public static class ScenarioVerificationExtensions
{
    /// <summary>Executes and verifies this scenario for the test that <paramref name="output"/> belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(this Scenario<TContext> scenario, ITestOutputHelper output)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario], output);
    }

    /// <summary>Executes and verifies this scenario using the supplied report configuration, for the test that <paramref name="output"/>
    /// belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(this Scenario<TContext> scenario, ITestOutputHelper output,
        ReportOptions reportOptions) where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return AssuranceAssert.VerifyAsync(scenario.Name, [scenario], output, reportOptions);
    }

    internal static Task<AssuranceVerificationResult> VerifyWithCancellationAsync<TContext>(this Scenario<TContext> scenario, ITestOutputHelper output,
        ReportOptions reportOptions, CancellationToken cancellationToken) where TContext : notnull =>
        AssuranceVerification.VerifyAsync(scenario.Name, [scenario], output, reportOptions, cancellationToken);
}
