using Ahr.Assurance.Adapters.Conformance;

namespace Ahr.Assurance.TUnit.Tests;

public sealed class TUnitAdapterConformanceTests
{
    public static IEnumerable<string> Cases() => AdapterConformance.CaseNames;

    [Test]
    [MethodDataSource(nameof(Cases))]
    public Task Conforms(string conformanceCase) => AdapterConformance.RunAsync(conformanceCase, new TUnitFramework());
}
