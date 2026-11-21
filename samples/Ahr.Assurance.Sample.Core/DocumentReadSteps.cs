using Ahr.Assurance.Sample.Application;

namespace Ahr.Assurance.Sample;

/// <summary>The steps of the document scenarios. Their checks return what was expected and what happened, so they work under any test framework.</summary>
public sealed class DocumentReadSteps : ScenarioSteps<DocumentReadContext>
{
    public static ScenarioAction<DocumentReadContext> TheDeviceIsOnline { get; } =
        CreateActionStep("The device is online", context =>
        {
            context.Source = new(context.ExpectedDocument);
            context.Cache = new();
            context.Reader = new(new FakeOnlineConnectivity(), context.Source, context.Cache);
        });

    public static ScenarioAction<DocumentReadContext> TheDeviceIsOffline { get; } =
        CreateActionStep("The device is offline", context =>
        {
            context.Source = new(context.ExpectedDocument);
            context.Cache = new();
            context.Reader = new(new FakeOfflineConnectivity(), context.Source, context.Cache);
        });

    public static ScenarioAction<DocumentReadContext> ASavedDocumentIsAvailable { get; } =
        CreateActionStep("A saved document is available", context => context.Cache.Seed(context.ExpectedDocument));

    public static ScenarioAction<DocumentReadContext> NoDocumentHasBeenSaved { get; } =
        CreateActionStep("No document has been saved", (context, token) => context.Cache.ClearAsync(token));

    public static ScenarioAction<DocumentReadContext> TheReaderOpensTheDocument { get; } =
        CreateActionStep("The reader opens the document", async (context, token) =>
            context.Result = await context.Reader.OpenAsync("handbook", token));

    public static ScenarioExpectation<DocumentReadContext> TheDownloadedDocumentIsReturned { get; } =
        CreateExpectationStep("The expected document is returned", context =>
            TheDocument(context, "The downloaded document is returned."));

    public static ScenarioExpectation<DocumentReadContext> TheSavedDocumentIsReturned { get; } =
        CreateExpectationStep("The saved document is returned", context =>
            TheDocument(context, "The saved document is returned."));

    public static ScenarioExpectation<DocumentReadContext> TheDownloadedDocumentIsCached { get; } =
        CreateExpectationStep("The downloaded document is saved for later", async (context, token) =>
        {
            Document? saved = await context.Cache.FindAsync("handbook", token);
            return ExpectationResult.Check(saved == context.ExpectedDocument, "The cache holds the downloaded document.",
                context.ExpectedDocument.ToString(), saved?.ToString() ?? "nothing");
        });

    public static ScenarioExpectation<DocumentReadContext> NoDocumentIsReturned { get; } =
        CreateExpectationStep("No document is returned before the first download", context =>
            ExpectationResult.Check(context.Result is { Document: null }, "No document is returned.",
                "no document", context.Result?.Document?.ToString() ?? "no document"));

    public static ScenarioExpectation<DocumentReadContext> FirstDownloadGuidanceIsReturned { get; } =
        CreateExpectationStep("First-time offline readers are told to connect before downloading", context =>
        {
            const string guidance = "Connect to the internet to download this document for the first time.";
            return ExpectationResult.Check(context.Result?.Guidance == guidance, "The reader is told to connect first.",
                guidance, context.Result?.Guidance ?? "no guidance");
        });

    public static ScenarioExpectation<DocumentReadContext> TheWebSourceWasContacted { get; } =
        CreateExpectationStep("The web source is contacted for the first download", context =>
            Downloads(context, 1));

    public static ScenarioExpectation<DocumentReadContext> TheWebSourceWasNotContacted { get; } =
        CreateExpectationStep("The web source is not contacted while offline", context =>
            Downloads(context, 0));

    private static ExpectationResult TheDocument(DocumentReadContext context, string explanation) =>
        ExpectationResult.Check(context.Result?.Document == context.ExpectedDocument, explanation,
            context.ExpectedDocument.ToString(), context.Result?.Document?.ToString() ?? "no document");

    private static ExpectationResult Downloads(DocumentReadContext context, int expected) =>
        ExpectationResult.Check(context.Source.DownloadCalls == expected, $"The web source is contacted {expected} times.",
            $"{expected} downloads", $"{context.Source.DownloadCalls} downloads");
}
