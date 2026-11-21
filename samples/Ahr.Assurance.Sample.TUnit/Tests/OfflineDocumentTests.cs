namespace Ahr.Assurance.Sample.Tests;

/// <summary>The shared document scenarios, verified under TUnit.</summary>
public sealed partial class OfflineDocumentTests
{
    [Test]
    public Task AConnectedReaderDownloadsAndSavesADocument() => DocumentReadScenarios.ConnectedReader.VerifyAsync();

    [Test]
    public Task AReturningOfflineReaderOpensASavedDocument() => DocumentReadScenarios.ReturningOfflineReader.VerifyAsync();

    [Test]
    public Task AFirstTimeOfflineReaderGetsConnectFirstGuidance() => DocumentReadScenarios.FirstTimeOfflineReader.VerifyAsync();

    [Test]
    public Task AReaderSeesASavedDocument() => DocumentReadScenarios.ReaderSeesSavedDocument.VerifyAsync();
}
