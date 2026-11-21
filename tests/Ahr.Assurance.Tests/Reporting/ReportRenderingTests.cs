using System.Collections.Immutable;
using System.Text;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests.Reporting;

public sealed class ReportRenderingTests
{
    [Fact]
    public void HtmlEncodesAllSuppliedContentAndUsesNoExternalAssets()
    {
        const string untrusted = "<script>alert(\"customer\")</script>";
        AssuranceReport report = new(Run(new(AssuranceStatus.Unsatisfied, untrusted, untrusted, untrusted)), [new("execution-error", untrusted)]);
        StakeholderReportWriter writer = new();
        var html = writer.Render(report, untrusted, developer: new(writer.Diagnostics(report.Diagnostics)));

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("src=", html);
        Assert.DoesNotContain("https://", html);
        Assert.Contains("Execution error [execution-error]", html);
        Assert.Contains("Expected", html);
        Assert.Contains("Actual", html);
    }

    [Fact]
    public void TracedValuesShowInTheDeveloperViewOnly()
    {
        AssuranceRunResult run = new("Traced input", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [
            new("policy", "A policy file is read", null,
                [new("A policy file", StepKind.Given, new(AssuranceStatus.Satisfied, "Done"), TimeSpan.Zero)
                {
                    Parameters = [new("yaml", "rules:\n  - <tests-first>"), new("strict", "True")]
                }])
        ]);
        StakeholderReportWriter writer = new();

        var business = writer.Render(new(run, []), "Business");
        var developer = writer.Render(new(run, []), "Developer", developer: new(""));

        Assert.DoesNotContain("yaml = ", business);
        Assert.Contains("<pre class=\"values\">yaml = rules:\n  - &lt;tests-first&gt;\nstrict = True</pre>", developer);
    }

    [Fact]
    public void PlainTextCountsOneScenarioInAGroupInTheSingular()
    {
        BusinessContext business = BusinessContext.Define("offline", "Offline reading", "Reading saved articles without a connection").Capability("read", "Read offline").Outcome("available", "Saved articles open offline").Build();
        AssuranceRunResult run = new("One journey", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [
            new("reader", "A reader opens a saved article", business,
                [new("Opened", StepKind.Then, new(AssuranceStatus.Satisfied, "Opened"), TimeSpan.Zero, null, StepKeyword.Then)])
        ]);

        var text = new PlainTextReportWriter().Render(new(run, []));

        Assert.Contains("Business group: Offline reading (offline); 1 associated scenario\n", text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void PlainTextIndentsEachFurtherLineOfAStepsEvidenceUnderIt()
    {
        AssuranceRunResult run = new("Multi-line evidence", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [
            new("total", "A basket shows its total", null,
                [new("The total is shown", StepKind.Then, new(AssuranceStatus.Unsatisfied, "Values differ\nExpected: 12\r\nActual:   10"), TimeSpan.Zero)])
        ]);

        var text = new PlainTextReportWriter().Render(new(run, [])).ReplaceLineEndings("\n");

        Assert.Contains("  Evidence: Values differ\n    Expected: 12\n    Actual:   10\n", text);
    }

    [Fact]
    public void PlainTextLeavesSuiteCompletionToTheReport()
    {
        AssuranceReport report = new(Run(new(AssuranceStatus.Satisfied, "Completed")), []);
        VerificationEvidence evidence = new("verification", "correlation", "tests", null, null, VerificationExecutionState.Completed, report, []);

        var text = new PlainTextReportWriter().RenderEvidence(evidence);

        Assert.Contains("Execution: Completed", text);
        Assert.DoesNotContain("Suite completion", text);
    }

    [Fact]
    public void TextLengthMustBePositive() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new StakeholderReportWriter(maxTextLength: 0));

    [Fact]
    public void PlainTextSummarizesMetadataKeywordsPartialEvidenceAndArtifactsSafely()
    {
        BusinessContext business = BusinessContext.Define("Keep readers connected\u001b[31m")
            .Capability("offline", "Read offline")
            .Outcome("saved", "Saved content is available")
            .Rule("no-fetch", "Do not fetch while offline")
            .Build();
        AssuranceRunResult run = new("Partial run", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [
            new("offline", "First-time offline reader", business,
            [
                new("Guidance", StepKind.Then,
                    new(AssuranceStatus.NotEvaluated, "Execution stopped", "Connect\u001b", "No guidance"),
                    TimeSpan.Zero, "Offer useful guidance", StepKeyword.And)
            ])
        ]);
        AssuranceReport report = new(run, [new("execution-error", "cache unavailable")], [new("HTML", "/tmp/report.html")]);

        var text = new PlainTextReportWriter(maxCharacters: 4_000).Render(report);

        Assert.Contains("Business context: Keep readers connected[31m", text);
        Assert.Contains("Capability: Read offline (offline)", text);
        Assert.Contains("Outcome: Saved content is available (saved)", text);
        Assert.Contains("Rule: Do not fetch while offline (no-fetch)", text);
        Assert.Contains("And Guidance — NotEvaluated", text);
        Assert.Contains("Expected: Connect", text);
        Assert.Contains("Actual: No guidance", text);
        Assert.Contains("Diagnostic: execution-error: cache unavailable", text);
        Assert.Contains("Artifact (HTML): /tmp/report.html", text);
        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain("Risk", text);
        Assert.DoesNotContain("Objective", text);
        Assert.DoesNotContain("Not specified", text);

        var deliveryFailureText = new PlainTextReportWriter().Render(new(
            Run(new(AssuranceStatus.Satisfied, "Completed")),
            [new("report-write-error", "The secondary output could not be written.")]));
        Assert.Contains("Verdict: Satisfied", deliveryFailureText);
        Assert.Contains("Diagnostic: report-write-error", deliveryFailureText);
        Assert.DoesNotContain("Execution incomplete", deliveryFailureText);

        var bounded = new PlainTextReportWriter(maxCharacters: 128).Render(report);
        Assert.Equal(128, bounded.Length);
        Assert.EndsWith("[summary truncated]", bounded);
    }

    [Fact]
    public async Task HtmlReporterLeavesCallerStreamOpenAndEncodesDiagnostics()
    {
        using MemoryStream stream = new();
        AssuranceReport report = new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, []),
            [new("<code>", "<message>", "<type>")]);
        await new StakeholderReportWriter().WriteAsync(report, stream, TestContext.Current.CancellationToken);

        Assert.True(stream.CanWrite);
        var html = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("&lt;code&gt;", html);
        Assert.Contains("&lt;message&gt;", html);
        Assert.Contains("&lt;type&gt;", html);
    }

    private static BusinessContext Business() =>
        BusinessContext.Define("Retain customer access")
            .Capability("renewal", "Renewal")
            .Outcome("access", "Customer retains access")
            .Build();

    private static AssuranceRunResult Run(ExpectationResult expectation) =>
        new("Run <unsafe>", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1),
            [new("scenario", "Scenario <unsafe>", Business(),
                [new("Observation <unsafe>", StepKind.Then, expectation, TimeSpan.Zero)])]);
}
