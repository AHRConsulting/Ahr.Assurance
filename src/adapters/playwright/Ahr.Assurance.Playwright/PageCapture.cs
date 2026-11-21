using Ahr.Assurance.Execution;
using Ahr.Foundation;
using Microsoft.Playwright;

namespace Ahr.Assurance.Playwright;

internal sealed class PageCapture(IPage page, PlaywrightCaptureOptions options) : IStepObserver
{
    private bool _tracing;

    public async Task ScenarioStartingAsync(string scenario, CancellationToken cancellationToken)
    {
        if (options.Trace == TraceCapture.None)
        {
            return;
        }

        _ = await page.Context.Tracing.StartAsync(new() { Title = scenario, Screenshots = true, Snapshots = true }).ConfigureAwait(false);
        _tracing = true;
    }

    public async Task StepFinishedAsync(StepOutcome step, CancellationToken cancellationToken)
    {
        if (options.Screenshots == ScreenshotCapture.None || (options.Screenshots == ScreenshotCapture.OnFailure && !step.Failed))
        {
            return;
        }

        Result<byte[]> screenshot = await Result.TryAsync(() => page.ScreenshotAsync(new() { FullPage = options.FullPage }),
            static exception => exception is PlaywrightException).ConfigureAwait(false);
        _ = screenshot.Match(
            image => Attach.Bytes($"Screenshot after: {step.Step}", image, "image/png"),
            error => Attach.Text($"Screenshot after: {step.Step} (unavailable).txt", error.Message));
    }

    public async Task ScenarioFinishedAsync(ScenarioOutcome scenario, CancellationToken cancellationToken)
    {
        if (!_tracing)
        {
            return;
        }

        _tracing = false;
        if (options.Trace == TraceCapture.OnFailure && !scenario.Failed)
        {
            await page.Context.Tracing.StopAsync().ConfigureAwait(false);
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"ahr-assurance-trace-{Guid.NewGuid():N}.zip");
        try
        {
            await page.Context.Tracing.StopAsync(new() { Path = path }).ConfigureAwait(false);
            Attach.Bytes("Playwright trace.zip", await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), "application/zip");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
