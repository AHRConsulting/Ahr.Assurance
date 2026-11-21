using System.Net;
using Ahr.Assurance.Playwright;
using Ahr.Assurance.Sample.Application;
using Microsoft.Playwright;

namespace Ahr.Assurance.Sample.Tests;

/// <summary>The document scenarios through a page in a headless Chromium, with a screenshot after each step.
/// Skipped when Playwright's Chromium isn't installed.</summary>
public sealed class DocumentPageTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        try
        {
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch (PlaywrightException exception)
        {
            Assert.Skip("Playwright's Chromium isn't installed: " + exception.Message.Split('\n')[0]);
        }

        _page = await _browser.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    [Fact]
    public async Task AnOfflineReaderSeesTheSavedHandbook()
    {
        using IDisposable capture = AssurancePlaywright.Capture(_page, new() { Screenshots = ScreenshotCapture.AfterEachStep });
        _ = await Scenario.Define<DocumentReadContext>("An offline reader sees the saved handbook", DocumentReadBusiness.ReturningOfflineReader)
            .Given(DocumentReadSteps.TheDeviceIsOffline)
            .And(DocumentReadSteps.ASavedDocumentIsAvailable)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .And("The reader's page shows the result", context => _page.SetContentAsync(Render(context.Result)))
            .Then("The handbook is on the page", async _ =>
                Assert.Equal("Offline-ready field handbook", await _page.InnerTextAsync("#content")))
            .VerifyAsync();
    }

    [Fact]
    public async Task AFirstTimeOfflineReaderSeesConnectFirstGuidance()
    {
        using IDisposable capture = AssurancePlaywright.Capture(_page, new() { Screenshots = ScreenshotCapture.AfterEachStep });
        _ = await Scenario.Define<DocumentReadContext>("A first-time offline reader sees connect-first guidance", DocumentReadBusiness.FirstTimeOfflineReader)
            .Given(DocumentReadSteps.TheDeviceIsOffline)
            .And(DocumentReadSteps.NoDocumentHasBeenSaved)
            .When(DocumentReadSteps.TheReaderOpensTheDocument)
            .And("The reader's page shows the result", context => _page.SetContentAsync(Render(context.Result)))
            .Then("The guidance is on the page", async _ =>
                Assert.Equal("Connect to the internet to download this document for the first time.", await _page.InnerTextAsync("#guidance")))
            .VerifyAsync();
    }

    private static string Render(DocumentReadResult? result)
    {
        if (result?.Document is { } document)
        {
            return $"<h1>{WebUtility.HtmlEncode(document.Id)}</h1><p id='content'>{WebUtility.HtmlEncode(document.Content)}</p>";
        }

        return $"<h1>Not saved yet</h1><p id='guidance'>{WebUtility.HtmlEncode(result?.Guidance)}</p>";
    }
}
