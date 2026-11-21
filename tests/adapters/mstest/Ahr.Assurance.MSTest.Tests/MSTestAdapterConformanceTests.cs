using Ahr.Assurance.Adapters.Conformance;

namespace Ahr.Assurance.MSTest.Tests;

[TestClass]
public sealed class MSTestAdapterConformanceTests
{
    public static IEnumerable<object[]> Cases => AdapterConformance.CaseNames.Select(name => new object[] { name });

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public Task Conforms(string conformanceCase) => AdapterConformance.RunAsync(conformanceCase, new MSTestFramework(TestContext));
}
