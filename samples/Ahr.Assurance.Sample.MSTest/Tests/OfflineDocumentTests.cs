namespace Ahr.Assurance.Sample.Tests;

/// <summary>The shared document scenarios, verified under MSTest.</summary>
[TestClass]
public sealed partial class OfflineDocumentTests
{
    [TestMethod]
    public Task AConnectedReaderDownloadsAndSavesADocument() => DocumentReadScenarios.ConnectedReader.VerifyAsync();

    [TestMethod]
    public Task AReturningOfflineReaderOpensASavedDocument() => DocumentReadScenarios.ReturningOfflineReader.VerifyAsync();

    [TestMethod]
    public Task AFirstTimeOfflineReaderGetsConnectFirstGuidance() => DocumentReadScenarios.FirstTimeOfflineReader.VerifyAsync();

    [TestMethod]
    public Task AReaderSeesASavedDocument() => DocumentReadScenarios.ReaderSeesSavedDocument.VerifyAsync();
}
