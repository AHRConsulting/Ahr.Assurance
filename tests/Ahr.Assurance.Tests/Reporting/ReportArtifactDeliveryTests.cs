using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests.Reporting;

public sealed class ReportArtifactDeliveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ahr-assurance-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("../html")]
    [InlineData(".ht/ml")]
    [InlineData("html")]
    [InlineData(".")]
    public void InvalidFormatExtensionsAreRejected(string extension) =>
        _ = Assert.Throws<ArgumentException>(() => new LocalFileArtifactDestination(_directory, extension));

    [Fact]
    public async Task CleanupFailureDoesNotMaskOriginalReporterFailure()
    {
        AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() =>
            LocalFileArtifactDestination.ForFile(Path.Combine(_directory, "report.txt"))
                .WriteAsync(new CleanupFailingReporter(new IOException("render failed")),
                new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, []), []),
                Path.Combine(_directory, "report.txt"), TestContext.Current.CancellationToken));

        Assert.Equal("render failed", failure.InnerExceptions[0].Message);
        Assert.True(failure.InnerExceptions[1] is IOException or UnauthorizedAccessException);
    }

    [Fact]
    public async Task CancellationRemainsUnchangedEvenWhenCleanupFails()
    {
        OperationCanceledException expected = new("Canceled");
        OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            LocalFileArtifactDestination.ForFile(Path.Combine(_directory, "report.txt"))
                .WriteAsync(new CleanupFailingReporter(expected),
                new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, []), []),
                Path.Combine(_directory, "report.txt"), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.True(actual.Data["ReportCleanupFailure"] is IOException or UnauthorizedAccessException);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class CleanupFailingReporter(Exception failure) : IAssuranceReporter
    {
        public Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken)
        {
            var path = Assert.IsType<FileStream>(destination).Name;
            File.Delete(path);
            _ = Directory.CreateDirectory(path);
            throw failure;
        }
    }
}
