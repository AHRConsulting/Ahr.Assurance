namespace Ahr.Assurance.Sample.Tests;

/// <summary>The shared document scenarios, verified under MSTest 3, which passes each test its TestContext.</summary>
[TestClass]
public sealed partial class OfflineDocumentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task AConnectedReaderDownloadsAndSavesADocument() => DocumentReadScenarios.ConnectedReader.VerifyAsync(TestContext);

    [TestMethod]
    public Task AReturningOfflineReaderOpensASavedDocument() => DocumentReadScenarios.ReturningOfflineReader.VerifyAsync(TestContext);

    [TestMethod]
    public Task AFirstTimeOfflineReaderGetsConnectFirstGuidance() => DocumentReadScenarios.FirstTimeOfflineReader.VerifyAsync(TestContext);

    [TestMethod]
    public Task AReaderSeesASavedDocument() => DocumentReadScenarios.ReaderSeesSavedDocument.VerifyAsync(TestContext);
}
