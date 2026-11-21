using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Microsoft.Playwright;
using Xunit.Sdk;

namespace Ahr.Assurance.Playwright.Tests;

/// <summary>A scenario that drives a browser page, verified while the page may be captured, and what its verification left behind.</summary>
public sealed class PageUnderTest(IPage page, string root)
{
    private readonly RecordingSink _sink = new();
    private Func<ReportOptions, Task<AssuranceVerificationResult>>? _verify;

    public IPage Page => page;

    /// <summary>What to capture while the scenario is verified; nothing is captured when it isn't set.</summary>
    public PlaywrightCaptureOptions? Capture { get; set; }

    public Exception? Failure { get; private set; }

    public ImmutableArray<EvidenceAttachment> Attachments => _sink.Single.Attachments;

    /// <summary>The scenario's step by its position, counting from 1.</summary>
    public StepResult Step(int number) => _sink.Single.Report.Run.Scenarios[0].Steps[number - 1];

    public string PathOf(EvidenceAttachment attachment) => Path.Combine(root, "attachments", attachment.Reference);

    public void Use(Scenario scenario) => _verify = scenario.VerifyAsync;

    public async Task VerifyAsync()
    {
        Func<ReportOptions, Task<AssuranceVerificationResult>> verify = _verify ?? throw new InvalidOperationException("No scenario to verify was given.");
        ReportOptions options = new() { DefaultOutputDirectory = root, TextOutput = AssuranceTextVerbosity.Off, Sinks = [_sink] };
        try
        {
            if (Capture is { } capture)
            {
                using (AssurancePlaywright.Capture(page, capture))
                {
                    _ = await verify(options);
                }
            }
            else
            {
                _ = await verify(options);
            }
        }
        catch (Exception exception)
        {
            Failure = exception;
        }
    }

    private sealed class RecordingSink : IAssuranceEvidenceSink
    {
        private readonly List<VerificationEvidence> _evidence = [];
        public string Name => "recording";
        public VerificationEvidence Single => Assert.Single(_evidence);

        public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
        {
            _evidence.Add(evidence);
            return Task.FromResult(EvidenceDeliveryResult.Empty);
        }
    }
}

public sealed class PageSteps : ScenarioSteps<PageUnderTest>
{
    public static ScenarioAction<PageUnderTest> ABasketWhoseTotalShouldBe(string total) =>
        CreateActionStep($"A basket page whose check expects a total of {total}", context => context.Use(
            Scenario.Define("A basket shows its total")
                .Given("A basket page", () => { })
                .When("The total is read", () => { })
                .Then($"The total is {total}", async () => Assert.Equal(total, await context.Page.InnerTextAsync("#total")))));

    public static ScenarioAction<PageUnderTest> ThePageIsCaptured(ScreenshotCapture screenshots, TraceCapture trace) =>
        CreateActionStep($"The page is captured with screenshots {screenshots} and trace {trace}", context =>
            context.Capture = new() { Screenshots = screenshots, Trace = trace });

    public static ScenarioAction<PageUnderTest> ItIsVerified { get; } =
        CreateActionStep("The scenario is verified", context => context.VerifyAsync());

    public static ScenarioExpectation<PageUnderTest> TheTestPasses { get; } =
        CreateAssertionStep("The test passes", context => Assert.Null(context.Failure));

    public static ScenarioExpectation<PageUnderTest> TheTestFails { get; } =
        CreateAssertionStep("The test fails the xUnit way", context => Assert.IsAssignableFrom<XunitException>(context.Failure));

    public static ScenarioExpectation<PageUnderTest> StepIs(int number, AssuranceStatus status) =>
        CreateAssertionStep($"Step {number} is {status}", context => Assert.Equal(status, context.Step(number).Evaluation.Status));
}

/// <summary>What the Playwright captures promise a team testing through a browser.</summary>
public static class PlaywrightBehaviour
{
    public static BusinessContext Captures { get; } = BusinessContext.Define("playwright-captures", "Browser captures",
            "A step that fails in a browser comes with a screenshot of the page, and a trace when the team asks for one, so a reader sees what the browser showed.")
        .Capability("captures", "Screenshots and traces")
        .Outcome("seen", "A reader sees the page each step saw")
        .Build();

    public static BusinessContext Checks { get; } = BusinessContext.Define("playwright-checks", "Playwright checks",
            "A failed Playwright expectation is a failed check about the page; any other Playwright error means the scenario couldn't run.")
        .Capability("expectations", "Playwright expectations")
        .Outcome("honest-verdicts", "A wrong page and a broken test are never confused")
        .Build();
}
