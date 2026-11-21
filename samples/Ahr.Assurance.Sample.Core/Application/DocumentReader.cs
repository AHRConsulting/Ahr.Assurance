namespace Ahr.Assurance.Sample.Application;

public sealed record Document(string Id, string Content);

public sealed record DocumentReadResult(Document? Document, string? Guidance);

public interface IConnectivity
{
    bool IsOnline { get; }
}

public interface IDocumentSource
{
    Task<Document> DownloadAsync(string documentId, CancellationToken cancellationToken);
}

public interface IDocumentCache
{
    Task<Document?> FindAsync(string documentId, CancellationToken cancellationToken);

    Task SaveAsync(Document document, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class DocumentReader(
    IConnectivity connectivity,
    IDocumentSource source,
    IDocumentCache cache)
{
    public async Task<DocumentReadResult> OpenAsync(string documentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        Document? cached = await cache.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return new(cached, null);
        }

        if (!connectivity.IsOnline)
        {
            return new(null, "Connect to the internet to download this document for the first time.");
        }

        Document downloaded = await source.DownloadAsync(documentId, cancellationToken).ConfigureAwait(false);
        await cache.SaveAsync(downloaded, cancellationToken).ConfigureAwait(false);
        return new(downloaded, null);
    }
}
