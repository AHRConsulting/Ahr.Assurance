using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Microsoft.Playwright;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.Playwright.Tests.PageSteps;

namespace Ahr.Assurance.Playwright.Tests;

/// <summary>Runs against a real headless Chromium. Skipped when Playwright's browsers aren't installed.</summary>
public sealed class PlaywrightCaptureTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private string _root = "";

    public async ValueTask InitializeAsync()
    {
        _root = Assurance.Tests.TestReports.TemporaryRoot();
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
        await _page.SetContentAsync("<h1>Basket</h1><p id='total'>5</p>");
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public Task AFailedStepGetsAScreenshotByDefault() =>
        Define("A failed step gets a screenshot of the page by default", PlaywrightBehaviour.Captures)
            .Given(ABasketWhoseTotalShouldBe("6"))
            .And("The page is captured with the default options", context => context.Capture = new())
            .When(ItIsVerified)
            .Then(TheTestFails)
            .And("A PNG screenshot is attached to the failed check", context =>
            {
                EvidenceAttachment screenshot = Assert.Single(context.Attachments);
                Assert.Equal(("image/png", 2, "Screenshot after: The total is 6"), (screenshot.MediaType, screenshot.StepIndex, screenshot.Name));
                Assert.True(new FileInfo(context.PathOf(screenshot)).Length > 0);
            })
            .VerifyAsync();

    [Fact]
    public Task ScreenshotsCanFollowEveryStep() =>
        Define("Screenshots can follow every step", PlaywrightBehaviour.Captures)
            .Given(ABasketWhoseTotalShouldBe("5"))
            .And(ThePageIsCaptured(ScreenshotCapture.AfterEachStep, TraceCapture.None))
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("Every step has a screenshot", context => Assert.Equal([0, 1, 2], context.Attachments.Select(attachment => attachment.StepIndex)))
            .VerifyAsync();

    [Theory]
    [InlineData(TraceCapture.OnFailure, 0)]
    [InlineData(TraceCapture.Always, 1)]
    public Task ATraceIsAttachedWhenAsked(TraceCapture trace, int traces) =>
        Define("A passing scenario's trace is attached to the scenario only when traces are always kept", PlaywrightBehaviour.Captures)
            .Given(ABasketWhoseTotalShouldBe("5"))
            .And(ThePageIsCaptured(ScreenshotCapture.None, trace))
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And($"{traces} trace is attached, as a zip under the scenario", context =>
            {
                Assert.Equal(traces, context.Attachments.Length);
                Assert.All(context.Attachments, attachment => Assert.Equal(("application/zip", null), (attachment.MediaType, attachment.StepIndex)));
            })
            .VerifyAsync();

    [Fact]
    public Task NothingIsCapturedOutsideTheCapture() =>
        Define("Nothing is captured once the capture has ended", PlaywrightBehaviour.Captures)
            .Given(ABasketWhoseTotalShouldBe("5"))
            .And("A capture of every step that has already ended", context =>
            {
                using (AssurancePlaywright.Capture(context.Page, new() { Screenshots = ScreenshotCapture.AfterEachStep }))
                {
                }
            })
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("Nothing is attached", context => Assert.Empty(context.Attachments))
            .VerifyAsync();

    [Fact]
    public Task AFailedPlaywrightExpectationIsAFailedCheck() =>
        Define("A failed Playwright expectation is a failed check, and the checks after it still run", PlaywrightBehaviour.Checks)
            .Given("A basket page whose total is checked with Playwright's Expect, followed by a check of its heading", context => context.Use(
                Scenario.Define("A basket's total is checked with Playwright")
                    .Given("A basket page", () => { })
                    .When("The total is read", () => { })
                    .Then("The total is 6", () => Assertions.Expect(context.Page.Locator("#total")).ToHaveTextAsync("6", new() { Timeout = 200 }))
                    .And("The heading is shown", () => Assertions.Expect(context.Page.Locator("h1")).ToBeVisibleAsync())))
            .When(ItIsVerified)
            .Then(TheTestFails)
            .And(StepIs(3, Unsatisfied))
            .And(StepIs(4, Satisfied))
            .VerifyAsync();

    [Fact]
    public Task APlaywrightErrorThatIsNotAnExpectationStopsTheScenario() =>
        Define("A Playwright error that isn't an expectation stops the scenario", PlaywrightBehaviour.Checks)
            .Given("A basket page whose script throws an error", context => context.Use(
                Scenario.Define("A basket page's script fails")
                    .Given("A basket page", () => { })
                    .When("A script fails", () => context.Page.EvaluateAsync("() => { throw new Error('boom') }"))
                    .Then("The total is 5", async () => Assert.Equal("5", await context.Page.InnerTextAsync("#total")))))
            .When(ItIsVerified)
            .Then(TheTestFails)
            .And(StepIs(2, NotEvaluated))
            .And(StepIs(3, NotEvaluated))
            .VerifyAsync();

    private Scenario<PageUnderTest> Define(string name, BusinessContext business) =>
        Scenario.Define(name, () => new PageUnderTest(_page!, _root), business);
}
