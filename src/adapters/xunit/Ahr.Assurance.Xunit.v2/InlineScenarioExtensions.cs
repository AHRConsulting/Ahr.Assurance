using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Xunit.Abstractions;

namespace Ahr.Assurance.Xunit;

/// <summary>Verification for local-variable scenarios, using the standard verification lifecycle.</summary>
public static class InlineScenarioExtensions
{
    /// <summary>Executes, reports, and asserts for the test that <paramref name="output"/> belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(output);
    }

    /// <summary>Verifies with explicit outputs, for the test that <paramref name="output"/> belongs to.</summary>
    /// <param name="scenario">The scenario to execute and verify.</param>
    /// <param name="output">The test's output helper, which xUnit passes to the test class's constructor.</param>
    /// <param name="reportOptions">Report outputs and text-output policy.</param>
    public static Task<AssuranceVerificationResult> VerifyAsync(this Scenario scenario, ITestOutputHelper output, ReportOptions reportOptions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Definition.VerifyAsync(output, reportOptions);
    }
}
