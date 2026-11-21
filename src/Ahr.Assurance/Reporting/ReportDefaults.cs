namespace Ahr.Assurance.Reporting;

internal static class ReportDefaults
{
    private static ReportOptions? _configured;

    internal static ReportOptions Current => Volatile.Read(ref _configured) ?? new();

    internal static void Configure(ReportOptions reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportOptions);
        if (Interlocked.CompareExchange(ref _configured, reportOptions, null) is not null)
        {
            throw new InvalidOperationException("Default Assurance report options can only be configured once per test process.");
        }
    }
}
