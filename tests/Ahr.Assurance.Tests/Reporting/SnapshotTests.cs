using System.Text.Json;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public void JsonPreservesTypedMetadataKeywordsEvidenceTimingDiagnosticsAndNulls()
    {
        VerificationEvidence original = Evidence();
        var json = EvidenceDocumentJson.Serialize(original);
        VerificationEvidence restored = EvidenceDocumentJson.Read(json).Value;
        Assert.Equal(json, EvidenceDocumentJson.Serialize(restored));
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"keyword\": \"And\"", json);
        Assert.Contains("\"expected\": null", json);
        Assert.Equal(original.VerificationId, restored.VerificationId);
        Assert.Equal("correlation", restored.CorrelationId);
        Assert.Equal("test-id", restored.TestId);
        AssuranceReport report = restored.Report;
        Assert.Equal("business", report.Run.Scenarios[0].Business?.Id);
        Assert.Equal("risk", report.Run.Scenarios[0].Business?.Risks.Single().Id);
        Assert.Equal(TimeSpan.FromTicks(1234), report.Run.Scenarios[0].Steps[1].Duration);
        Assert.Null(report.Run.Scenarios[0].Steps[0].Evaluation.Expected);
        Assert.Equal("report-write-error", report.Diagnostics.Single().Code);
        Assert.Empty(report.Artifacts);
        Assert.DoesNotContain("file.html", json);
        var html = new StakeholderReportWriter().Render(report, "Read", developer: new(""));
        Assert.Contains("Saved documents", html);
        Assert.Contains("saved &lt;value&gt;", html);
        Assert.Contains("And Saved copy", html);
        Assert.DoesNotContain("<value>", html);
    }

    [Fact]
    public void RejectsUnknownVersionMissingPropertiesEnumsAndInvalidReportData()
    {
        VerificationEvidence evidence = Evidence();
        var json = EvidenceDocumentJson.Serialize(evidence);
        Assert.True(EvidenceDocumentJson.Read(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal)).IsFailure);
        Assert.True(EvidenceDocumentJson.Read("{}").IsFailure);
        Assert.True(EvidenceDocumentJson.Read("{\"schemaVersion\":\"2\"}").IsFailure);
        Assert.True(EvidenceDocumentJson.Read("{\"schemaVersion\":2}").IsFailure);
        Assert.True(EvidenceDocumentJson.Read(json.Replace("\"Satisfied\"", "\"PassMaybe\"", StringComparison.Ordinal)).IsFailure);
        Assert.True(EvidenceDocumentJson.Read(json.Replace("\"evidence\":", "\"unexpected\": 1, \"evidence\":", StringComparison.Ordinal)).IsFailure);
        ReportSnapshot snapshot = EvidenceDocument.Capture(evidence).Evidence;
        Assert.True((snapshot with
        {
            DurationTicks = -1
        }).ToReport().IsFailure);
        Assert.True((snapshot with
        {
            Scenarios = [null!]
        }).ToReport().IsFailure);
    }

    [Fact]
    public void CanceledEvidenceKeepsPartialObservationsAndNeverClaimsSuiteCompletion()
    {
        VerificationEvidence evidence = Evidence() with
        {
            ExecutionState = VerificationExecutionState.Canceled
        };
        VerificationEvidence restored = EvidenceDocumentJson.Read(EvidenceDocumentJson.Serialize(evidence)).Value;
        Assert.Equal(VerificationExecutionState.Canceled, restored.ExecutionState);
        Assert.Equal(2, restored.Report.Run.Scenarios[0].Steps.Length);
        Assert.Contains("Execution: Canceled", new PlainTextReportWriter().RenderEvidence(restored));
    }

    private static VerificationEvidence Evidence()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        BusinessContext business = BusinessContext.Define("business", "Saved documents", "Read while offline")
            .Capability("read", "Read documents").Outcome("saved", "Return saved copy")
            .Risk("risk", "Work interrupted").Rule("rule", "No web while offline").Objective("goal", "Keep working").Build();
        ScenarioResult scenario = new("read", "Read saved document", business,
        [
            new("Prepared", StepKind.Given, new(AssuranceStatus.Satisfied, "Ready"), TimeSpan.Zero, null, StepKeyword.Given),
            new("Saved copy", StepKind.Then, new(AssuranceStatus.Unsatisfied, "Observed", "saved <value>", "absent"),
                TimeSpan.FromTicks(1234), "Check cache", StepKeyword.And)
        ], "Offline reading");
        AssuranceReport report = new(new("Read", now, TimeSpan.FromTicks(1234), [scenario]),
            [new("report-write-error", "Example delivery error", "IOException")], [new("HTML", "file.html")]);
        return new(Guid.NewGuid().ToString("N"), "correlation", "Example.Tests", "test-id", "Fact",
            VerificationExecutionState.Completed, report, []);
    }
}
