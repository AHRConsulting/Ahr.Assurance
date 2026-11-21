namespace Ahr.Assurance.Sample;

/// <summary>The document scenarios, built from the shared steps. Each test project verifies them with its own adapter.</summary>
public static class DocumentReadScenarios
{
    private static readonly string _screen = Path.Combine(AppContext.BaseDirectory, "Screens", "document-screen.png");

    public static Scenario<DocumentReadContext> ConnectedReader =>
        Scenario.Define<DocumentReadContext>("A connected reader downloads and saves a document", DocumentReadBusiness.ConnectedReader,
                id: "connected-reader",
                description: "The first online read returns a document and stores it for later.")
            .Given(DocumentReadSteps.TheDeviceIsOnline)
            .And(DocumentReadSteps.NoDocumentHasBeenSaved)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .Then(DocumentReadSteps.TheDownloadedDocumentIsReturned)
            .And(DocumentReadSteps.TheDownloadedDocumentIsCached)
            .And(DocumentReadSteps.TheWebSourceWasContacted);

    public static Scenario<DocumentReadContext> ReturningOfflineReader =>
        Scenario.Define<DocumentReadContext>("A returning offline reader opens a saved document", DocumentReadBusiness.ReturningOfflineReader,
                id: "returning-offline-reader",
                description: "A saved copy is available without contacting the web.")
            .Given(DocumentReadSteps.TheDeviceIsOffline)
            .And(DocumentReadSteps.ASavedDocumentIsAvailable)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .Then(DocumentReadSteps.TheSavedDocumentIsReturned)
            .And(DocumentReadSteps.TheWebSourceWasNotContacted);

    public static Scenario<DocumentReadContext> FirstTimeOfflineReader =>
        Scenario.Define<DocumentReadContext>("A first-time offline reader gets connect-first guidance", DocumentReadBusiness.FirstTimeOfflineReader,
                id: "first-time-offline-reader",
                description: "Without a saved copy, the reader receives actionable guidance instead of a web call.")
            .Given(DocumentReadSteps.TheDeviceIsOffline)
            .And(DocumentReadSteps.NoDocumentHasBeenSaved)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .Then(DocumentReadSteps.NoDocumentIsReturned)
            .And(DocumentReadSteps.FirstDownloadGuidanceIsReturned)
            .And(DocumentReadSteps.TheWebSourceWasNotContacted);

    /// <summary>Steps attach a log and a screenshot, which the report shows beside them.</summary>
    public static Scenario<DocumentReadContext> ReaderSeesSavedDocument =>
        Scenario.Define<DocumentReadContext>("A reader sees a saved document", DocumentReadBusiness.ReturningOfflineReader,
                id: "reader-sees-saved-document")
            .Given(DocumentReadSteps.TheDeviceIsOffline)
            .And(DocumentReadSteps.ASavedDocumentIsAvailable)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .Then("The document is shown", _ =>
            {
                Attach.Text("reader.log", "Opened from the local cache.");
                Attach.File(_screen, "Document screen");
            });
}
