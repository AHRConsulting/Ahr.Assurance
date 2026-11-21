using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed class ReportCommand(ReportApplication application, TextWriter output, TextWriter error)
{
    internal async Task<int> RunAsync(ReportRequest request, CancellationToken token)
    {
        try
        {
            Result<ReportCompletion, CommandError> result = await application.RunAsync(request, token).ConfigureAwait(false);
            return result.Match(completed =>
            {
                if (request.Config is { } config)
                {
                    output.WriteLine($"Config: {config.Path}");
                }
                foreach (var line in completed.Lines.IsDefault ? [] : completed.Lines)
                {
                    output.WriteLine(line);
                }
                output.WriteLine($"Recorded verifications: {completed.Count}\nSelected run: {completed.CorrelationId}\n"
                    + (completed.Tests is { } tests ? $"Tests: {tests.Describe(completed.Count)}" : "Suite completion: not established"));
                foreach ((var format, var path) in completed.Outputs.IsDefaultOrEmpty ? [("html", completed.IndexPath)] : completed.Outputs)
                {
                    output.WriteLine(format == "html" ? $"Assurance HTML: {path}" : $"Report {format}: {path}");
                }
                if (request.Contexts is { Length: > 0 } contexts)
                {
                    output.WriteLine($"Report shows business contexts: {string.Join(", ", contexts)}. The exit code reflects the whole run.");
                }
                else if (ReportApplication.Excluded(request) is { Length: > 0 } excluded)
                {
                    output.WriteLine($"Report leaves out business contexts: {string.Join(", ", excluded)}. The exit code reflects the whole run.");
                }

                foreach (CommandError failure in completed.FormatErrors)
                {
                    error.WriteLine($"Assurance report failed [{failure.Code}]: {failure.Message}");
                    if (failure.Detail is not null && (request.IncludeDiagnosticDetails || failure.Code == "format-build-error"))
                    {
                        error.WriteLine(failure.Detail);
                    }
                }

                return completed.ExitCode;
            }, failure =>
            {
                error.WriteLine($"Assurance report failed [{failure.Code}]: {failure.Message}");
                if (request.IncludeDiagnosticDetails && failure.Detail is not null)
                {
                    error.WriteLine(failure.Detail);
                }
                return 1;
            });
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Assurance report canceled; generated outputs may be partial.");
            return 130;
        }
    }
}
