using Ahr.Assurance.Sample.Application;

namespace Ahr.Assurance.Sample;

public sealed class FakeOnlineConnectivity : IConnectivity
{
    public bool IsOnline => true;
}

public sealed class FakeOfflineConnectivity : IConnectivity
{
    public bool IsOnline => false;
}

public sealed class FakeDocumentSource(Document document) : IDocumentSource
{
    private int _downloadCalls;

    public int DownloadCalls => Volatile.Read(ref _downloadCalls);

    public async Task<Document> DownloadAsync(string documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _downloadCalls);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return document.Id == documentId
            ? document
            : throw new InvalidOperationException($"No fake document is configured for '{documentId}'.");
    }
}

public sealed class FakeInMemoryDocumentCache : IDocumentCache
{
    private readonly Dictionary<string, Document> _documents = new(StringComparer.Ordinal);

    public Task<Document?> FindAsync(string documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _documents.TryGetValue(documentId, out Document? document);
        return Task.FromResult(document);
    }

    public Task SaveAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public void Seed(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[document.Id] = document;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.Clear();
        return Task.CompletedTask;
    }
}
