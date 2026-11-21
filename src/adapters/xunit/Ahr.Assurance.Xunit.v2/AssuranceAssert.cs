using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Xunit.Abstractions;

namespace Ahr.Assurance.Xunit;

/// <summary>Explicit multi-scenario verification entry point.</summary>
public static class AssuranceAssert
{
    /// <summary>Executes and verifies the scenarios for the test that <paramref name="output"/> belongs to.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios, ITestOutputHelper output)
        where TContext : notnull =>
        AssuranceVerification.VerifyAsync(name, scenarios, output, AssuranceXunitConfiguration.DefaultReportOptions);

    /// <summary>Executes and verifies the scenarios using the supplied report configuration, for the test that <paramref name="output"/>
    /// belongs to.</summary>
    /// <param name="name">The report and run name.</param>
    /// <param name="scenarios">Scenarios to execute.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios, ITestOutputHelper output,
        ReportOptions reportOptions) where TContext : notnull =>
        AssuranceVerification.VerifyAsync(name, scenarios, output, reportOptions);
}
