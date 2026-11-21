using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.MSTest;

/// <summary>Configures AHR Assurance for an MSTest test project.</summary>
public static class AssuranceMSTestConfiguration
{
    /// <summary>Sets the report options that verifications in this test process use when none are passed. Call it once, before
    /// the tests run.</summary>
    /// <param name="reportOptions">The report options to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reportOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The default report options are already set.</exception>
    public static void ConfigureDefault(ReportOptions reportOptions) => ReportDefaults.Configure(reportOptions);

    internal static ReportOptions DefaultReportOptions => ReportDefaults.Current;
}
