using System.Collections.Immutable;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed record TestFramework(string Name, string Display, string Adapter)
{
    internal static TestFramework Playwright { get; } = new("playwright", "Playwright", "Ahr.Assurance.Playwright");

    internal static TestFramework XunitV3 { get; } = new("xunit-v3", "xUnit v3", "Ahr.Assurance.Xunit.v3");

    internal static TestFramework XunitV2 { get; } = new("xunit-v2", "xUnit v2", "Ahr.Assurance.Xunit.v2");

    internal static TestFramework NUnit { get; } = new("nunit", "NUnit", "Ahr.Assurance.NUnit");

    internal static TestFramework MSTest { get; } = new("mstest", "MSTest 4", "Ahr.Assurance.MSTest");

    internal static TestFramework MSTestV3 { get; } = new("mstest-v3", "MSTest 3", "Ahr.Assurance.MSTest.v3");

    internal static TestFramework TUnit { get; } = new("tunit", "TUnit", "Ahr.Assurance.TUnit");

    internal static ImmutableArray<TestFramework> All { get; } = [XunitV3, XunitV2, NUnit, MSTest, MSTestV3, TUnit];

    internal static string Names => string.Join(", ", All.Append(Playwright).Select(framework => framework.Name));

    internal static Option<TestFramework> Named(string name) =>
        All.Append(Playwright).Where(framework => string.Equals(framework.Name, name, StringComparison.OrdinalIgnoreCase)).SingleOrNone();

    internal static Option<TestFramework> ByAdapter(string package) =>
        All.Where(framework => string.Equals(framework.Adapter, package, StringComparison.OrdinalIgnoreCase)).SingleOrNone();

    internal string Described => $"{Display} ({Name})";
}
