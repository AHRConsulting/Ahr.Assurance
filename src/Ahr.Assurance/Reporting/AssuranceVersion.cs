using System.Reflection;

namespace Ahr.Assurance.Reporting;

internal static class AssuranceVersion
{
    internal static string Current { get; } = Read();

    private static string Read()
    {
        var version = typeof(AssuranceVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var build = version.IndexOf('+', StringComparison.Ordinal);
        return build < 0 ? version : version[..build];
    }
}
