namespace Ahr.Assurance.Sample.Tests;

/// <summary>The shared document scenarios, verified under xUnit v2. Each test passes its ITestOutputHelper.</summary>
public sealed partial class OfflineDocumentTests(ITestOutputHelper output)
{
    [Fact]
    public Task AConnectedReaderDownloadsAndSavesADocument() => DocumentReadScenarios.ConnectedReader.VerifyAsync(output);

    [Fact]
    public Task AReturningOfflineReaderOpensASavedDocument() => DocumentReadScenarios.ReturningOfflineReader.VerifyAsync(output);

    [Fact]
    public Task AFirstTimeOfflineReaderGetsConnectFirstGuidance() => DocumentReadScenarios.FirstTimeOfflineReader.VerifyAsync(output);

    [Fact]
    public Task AReaderSeesASavedDocument() => DocumentReadScenarios.ReaderSeesSavedDocument.VerifyAsync(output);
}
