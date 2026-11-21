using System.ComponentModel;
using Ahr.Assurance.Reporting;
using Microsoft.Testing.Platform.Builder;

namespace Ahr.Assurance.TestingPlatform;

/// <summary>Registers AHR Assurance with Microsoft.Testing.Platform. The test application's generated entry point calls it.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestingPlatformBuilderHook
{
    /// <summary>Adds AHR Assurance's extensions to the test application.</summary>
    /// <param name="builder">The test application being built.</param>
    /// <param name="arguments">The test application's command-line arguments, which the platform's own option parsing reads.</param>
#pragma warning disable IDE0060
    public static void AddExtensions(ITestApplicationBuilder builder, string[] arguments) =>
        AssurancePlatform.AddExtensions(builder, () => ReportDefaults.Current);
#pragma warning restore IDE0060
}
