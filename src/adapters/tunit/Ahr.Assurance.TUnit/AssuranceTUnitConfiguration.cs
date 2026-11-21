using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.TUnit;

/// <summary>Configures AHR Assurance for a TUnit test project.</summary>
public static class AssuranceTUnitConfiguration
{
    /// <summary>Sets the report options that verifications in this test process use when none are passed. Call it once, before
    /// the tests run.</summary>
    /// <param name="reportOptions">The report options to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reportOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The default report options are already set.</exception>
    public static void ConfigureDefault(ReportOptions reportOptions) => ReportDefaults.Configure(reportOptions);

    internal static ReportOptions DefaultReportOptions => ReportDefaults.Current;
}
