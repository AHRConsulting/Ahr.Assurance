using System.Runtime.ExceptionServices;

namespace Ahr.Assurance.Reporting;

internal static class ReportArtifactWriter
{
    public static Task WriteAsync(
        IAssuranceReporter reporter,
        AssuranceReport report,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(report);
        return WriteAsync((stream, token) => reporter.WriteAsync(report, stream, token), path, cancellationToken);
    }

    public static async Task WriteAsync(
        Func<Stream, CancellationToken, Task> write,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("A report destination directory is required.", nameof(path));
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Path.GetRandomFileName());
        var ownsTemporary = false;
        Exception? failure = null;
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                ownsTemporary = true;
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (ownsTemporary)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanupFailure)
            {
                if (failure is OperationCanceledException cancellation)
                {
                    cancellation.Data["ReportCleanupFailure"] = cleanupFailure;
                }
                else
                {
                    failure = failure is null ? cleanupFailure : new AggregateException(
                        "Report writing and temporary artifact cleanup both failed.", failure, cleanupFailure);
                }
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
