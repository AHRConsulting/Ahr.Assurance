using Ahr.Assurance.Adapters.Conformance;

namespace Ahr.Assurance.NUnit.Tests;

public sealed class NUnitAdapterConformanceTests
{
    public static IEnumerable<string> Cases => AdapterConformance.CaseNames;

    [TestCaseSource(nameof(Cases))]
    public Task Conforms(string conformanceCase) => AdapterConformance.RunAsync(conformanceCase, new NUnitFramework());
}
