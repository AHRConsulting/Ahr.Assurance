using Ahr.Assurance.Execution;
using Microsoft.Playwright;

namespace Ahr.Assurance.Playwright;

/// <summary>Attaches a page's screenshots and trace to the evidence of the scenarios verified while a capture is in scope.</summary>
public static class AssurancePlaywright
{
    /// <summary>
    /// Captures <paramref name="page"/> for every scenario verified in the current test until the returned capture is disposed:
    /// by default a screenshot after a step that fails. Screenshots go under their step in the report, and a trace under its scenario.
    /// </summary>
    /// <param name="page">The page to capture.</param>
    /// <param name="options">What to capture; by default a screenshot after a failed step, and no trace.</param>
    public static IDisposable Capture(IPage page, PlaywrightCaptureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        return StepObservers.Add(new PageCapture(page, options ?? new()));
    }
}

/// <summary>When to attach a screenshot of the page.</summary>
public enum ScreenshotCapture
{
    /// <summary>No screenshots.</summary>
    None,

    /// <summary>After a step whose check failed or that stopped the scenario.</summary>
    OnFailure,

    /// <summary>After every step.</summary>
    AfterEachStep,
}

/// <summary>When to attach the Playwright trace of a scenario.</summary>
public enum TraceCapture
{
    /// <summary>No trace.</summary>
    None,

    /// <summary>Only for a scenario that failed.</summary>
    OnFailure,

    /// <summary>For every scenario.</summary>
    Always,
}

/// <summary>What a Playwright capture attaches to the evidence.</summary>
public sealed record PlaywrightCaptureOptions
{
    /// <summary>When to attach a screenshot. By default, after a failed step.</summary>
    public ScreenshotCapture Screenshots { get; init; } = ScreenshotCapture.OnFailure;

    /// <summary>When to attach the scenario's trace, viewable with <c>playwright show-trace</c>. Recording it slows the scenario. By default, never.</summary>
    public TraceCapture Trace { get; init; } = TraceCapture.None;

    /// <summary>Whether a screenshot covers the whole scrollable page rather than the visible part. By default, the visible part.</summary>
    public bool FullPage { get; init; }
}
