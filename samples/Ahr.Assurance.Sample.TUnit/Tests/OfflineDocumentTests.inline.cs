using Ahr.Assurance.Sample.Application;

namespace Ahr.Assurance.Sample.Tests;

public sealed partial class OfflineDocumentTests
{
    /// <summary>A scenario written in the test, checked with TUnit's Assert, once for each row.</summary>
    [Test]
    [Arguments(2)]
    [Arguments(3)]
    public Task OpeningADocumentAgainDownloadsItOnce(int opens)
    {
        Document handbook = new("handbook", "Offline-ready field handbook");
        FakeDocumentSource source = new(handbook);
        DocumentReader? reader = null;
        DocumentReadResult? last = null;

        return Scenario.Define("Opening a document again downloads it once", DocumentReadBusiness.ConnectedReader)
            .Given("A connected reader with no saved copy", () => reader = new(new FakeOnlineConnectivity(), source, new FakeInMemoryDocumentCache()))
            .When("The reader opens the handbook more than once", async token =>
            {
                for (var open = 0; open < opens; open++)
                {
                    last = await reader!.OpenAsync("handbook", token);
                }
            })
            .Then("The same document is returned", async () => await Assert.That(last?.Document).IsEqualTo(handbook))
            .And("The web source is contacted only once", async () => await Assert.That(source.DownloadCalls).IsEqualTo(1))
            .VerifyAsync();
    }
}
