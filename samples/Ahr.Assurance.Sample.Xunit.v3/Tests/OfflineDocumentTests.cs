namespace Ahr.Assurance.Sample.Tests;

/// <summary>The shared document scenarios, verified under xUnit.</summary>
public sealed partial class OfflineDocumentTests
{
    [Fact]
    public Task AConnectedReaderDownloadsAndSavesADocument() => DocumentReadScenarios.ConnectedReader.VerifyAsync();

    [Fact]
    public Task AReturningOfflineReaderOpensASavedDocument() => DocumentReadScenarios.ReturningOfflineReader.VerifyAsync();

    [Fact]
    public Task AFirstTimeOfflineReaderGetsConnectFirstGuidance() => DocumentReadScenarios.FirstTimeOfflineReader.VerifyAsync();

    [Fact]
    public Task AReaderSeesASavedDocument() => DocumentReadScenarios.ReaderSeesSavedDocument.VerifyAsync();
}
