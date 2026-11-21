namespace Ahr.Assurance.Sample;

public static class DocumentReadBusiness
{
    private static readonly Capability _downloadFirstCopy = new("download-first-copy", "Download a first copy while connected");
    private static readonly Capability _readSavedDocuments = new("read-saved-documents", "Read a saved document without a network");
    private static readonly Capability _guideFirstDownload = new("guide-first-download", "Guide readers without a saved copy");
    private static readonly Outcome _downloadReturned = new("download-returned", "The downloaded document is returned");
    private static readonly Outcome _laterCopySaved = new("later-copy-saved", "A downloaded document is saved for later");
    private static readonly Outcome _savedContentAvailable = new("saved-content-available", "Saved content is returned to the reader");
    private static readonly Outcome _firstDownloadGuidance = new("first-download-guidance", "First-time offline readers receive connect-first guidance");
    private static readonly BusinessRule _offlineDoesNotFetch = new("offline-does-not-fetch", "Offline reads never contact the web source");
    private static readonly BusinessRule _cacheAfterDownload = new("cache-after-download", "Downloaded content is stored in the cache");
    private static readonly Risk _downloadNotSaved = new("download-not-saved", "Downloaded content is unavailable for later reading");
    private static readonly Risk _offlineContentMissing = new("offline-content-missing", "A returning reader cannot access a saved document");
    private static readonly Risk _unhelpfulFirstUse = new("unhelpful-first-use", "A first-time reader is left without actionable guidance");
    private static readonly Objective _readerContinuity = new("reader-continuity", "Maintain reading continuity across connectivity changes");

    public static BusinessContext ConnectedReader { get; } =
        BusinessContext.Define("offline-document-access", "Offline document access",
                "Keep documents available when connectivity changes")
            .Reference("jira", "DOC-42", new Uri("https://example.atlassian.net/browse/DOC-42"))
            .Capability(_downloadFirstCopy)
            .Outcome(_downloadReturned)
            .Outcome(_laterCopySaved)
            .Rule(_cacheAfterDownload)
            .Risk(_downloadNotSaved)
            .Objective(_readerContinuity)
            .Build();

    public static BusinessContext ReturningOfflineReader { get; } =
        BusinessContext.Define("offline-document-access", "Offline document access",
                "Keep documents available when connectivity changes")
            .Reference("jira", "DOC-42", new Uri("https://example.atlassian.net/browse/DOC-42"))
            .Capability(_readSavedDocuments)
            .Outcome(_savedContentAvailable)
            .Rule(_offlineDoesNotFetch)
            .Risk(_offlineContentMissing)
            .Objective(_readerContinuity)
            .Build();

    public static BusinessContext FirstTimeOfflineReader { get; } =
        BusinessContext.Define("offline-document-access", "Offline document access",
                "Keep documents available when connectivity changes")
            .Reference("jira", "DOC-42", new Uri("https://example.atlassian.net/browse/DOC-42"))
            .Capability(_guideFirstDownload)
            .Outcome(_firstDownloadGuidance)
            .Rule(_offlineDoesNotFetch)
            .Risk(_unhelpfulFirstUse)
            .Objective(_readerContinuity)
            .Build();
}
