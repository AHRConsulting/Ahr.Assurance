using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class DeliveringReports
{
    [Fact]
    public Task AReportIsReplacedOnlyByACompleteReport() =>
        Scenario.Define<ReportFile>("A report is replaced only by a complete report", EvidenceCapabilities.Reports)
            .Given("A report of a satisfied run named 'First' is written", (report, token) => report.WriteAsync(new StakeholderReportWriter(), Satisfied, "First", token))
            .When("A report of an unsatisfied run named 'Second' is written over it", (report, token) =>
                report.WriteAsync(new StakeholderReportWriter(), Unsatisfied, "Second", token))
            .Then("The report shows only the second run, to its end", async (report, token) =>
            {
                var html = await File.ReadAllTextAsync(report.Path, token);
                Assert.Contains("Second", html);
                Assert.DoesNotContain(">First<", html);
                Assert.EndsWith("</footer></div></body></html>", html);
            })
            .And(ReportFileSteps.NothingElseIsLeft)
            .VerifyAsync();

    [Fact]
    public Task ACancelledReportLeavesTheEarlierOne() =>
        Scenario.Define<ReportFile>("A report cancelled before it is written leaves the earlier one", EvidenceCapabilities.Reports)
            .Given(ReportFileSteps.AnEarlierReport)
            .And("The host has already cancelled", report => report.Cancellation.Cancel())
            .WhenThrows<OperationCanceledException>("A new report is written over it", report =>
                report.WriteAsync(new StakeholderReportWriter(), Satisfied, "New", report.Cancellation.Token))
            .Then(ReportFileSteps.TheEarlierReportIsKept)
            .And(ReportFileSteps.NothingElseIsLeft)
            .VerifyAsync();

    [Fact]
    public Task AReportThatFailsLeavesTheEarlierOne() =>
        Scenario.Define<ReportFile>("A report that fails while it is written leaves the earlier one", EvidenceCapabilities.Reports)
            .Given(ReportFileSteps.AnEarlierReport)
            .WhenThrows<IOException>("A report that fails halfway is written over it", (report, token) =>
                report.WriteAsync(new BrokenReporter(), Satisfied, "New", token))
            .Then<IOException>("The failure is the reporter's own", (_, failure) => Assert.Equal("render failed", failure.Message))
            .And(ReportFileSteps.TheEarlierReportIsKept)
            .And(ReportFileSteps.NothingElseIsLeft)
            .VerifyAsync();

    [Fact]
    public Task ACancellationWhileWritingIsRethrownAsItWas() =>
        Scenario.Define<ReportFile>("A report cancelled while it is written leaves the earlier one, and the cancellation is rethrown as it was",
                EvidenceCapabilities.Reports)
            .Given(ReportFileSteps.AnEarlierReport)
            .WhenThrows<OperationCanceledException>("A report cancelled halfway is written over it", report =>
                report.WriteAsync(new CancelledReporter(report.HostCancellation), Satisfied, "New", report.Cancellation.Token))
            .Then<OperationCanceledException>("The host's own cancellation is rethrown", (report, cancellation) =>
                Assert.Same(report.HostCancellation, cancellation))
            .And(ReportFileSteps.TheEarlierReportIsKept)
            .And(ReportFileSteps.NothingElseIsLeft)
            .VerifyAsync();

    [Fact]
    public Task NoRunReportIsWrittenWithoutEvidence() =>
        Scenario.Define<RunUnderTest>("No run report is written for a run without evidence", EvidenceCapabilities.Reports)
            .Given("No evidence was recorded in the run", run => Assert.Empty(Directory.GetFiles(run.Root, "*.json")))
            .When("A test process with three tests, one failed, writes the run report", async (run, token) => run.Report = (await RunReport.WriteAsync(run.Options, run.Tests, null, writeReport: true, token)).GetValueOrDefault())
            .Then("No report is written", run => Assert.Null(run.Report))
            .And("No summary of the tests is kept", run => Assert.False(Directory.Exists(Path.Combine(run.Root, TestRunSummary.Folder))))
            .VerifyAsync();

    [Fact]
    public Task TheRunReportCountsEveryTest() =>
        Scenario.Define<RunUnderTest>("The run report counts every test from every test process, and only this run's evidence", EvidenceCapabilities.Reports,
                description: "Plain tests count as much as scenarios: a run with a failed plain test is not a passing run.")
            .Given("Evidence from this run and from another run", async (run, token) =>
            {
                await File.WriteAllTextAsync(Path.Combine(run.Root, "a.json"), EvidenceDocumentJson.Serialize(RunUnderTest.Evidence("run-1")), token);
                await File.WriteAllTextAsync(Path.Combine(run.Root, "b.json"), EvidenceDocumentJson.Serialize(RunUnderTest.Evidence("another-run")), token);
            })
            .And("One test process recorded its three tests, one failed, without writing the report", async (run, token) =>
                Assert.True((await RunReport.WriteAsync(run.Options, run.Tests, null, writeReport: false, token)).IsNone))
            .When("Another process with three tests, one failed, writes the run report titled 'Release 4.2'", async (run, token) =>
                run.Report = (await RunReport.WriteAsync(run.Options, run.Tests, "Release 4.2", writeReport: true, token)).GetValueOrDefault())
            .Then("The report is in the run's report folder", run => Assert.Equal(Path.Combine(run.Root, "report", "report.html"), run.Report))
            .And("It counts the tests of both processes and the evidence of this run only", async (run, token) =>
            {
                var html = await File.ReadAllTextAsync(run.Report!, token);
                Assert.Contains("<h1>Release 4.2</h1>", html);
                Assert.Contains("6 tests ran: 4 passed, 2 failed, 0 skipped. 1 recorded scenario evidence.", html);
            })
            .VerifyAsync();

    [Fact]
    public Task FailedPlainTestsKeepTheHeadlineFromSayingTheRunPassed() =>
        Scenario.Define<RunUnderTest>("Failed plain tests keep the headline from saying the run passed", EvidenceCapabilities.Reports,
                description: "Every scenario passed, but two plain tests failed: the report leads with that, not with a pass.")
            .Given("Evidence from this run whose one scenario passed", (run, token) =>
                File.WriteAllTextAsync(Path.Combine(run.Root, "a.json"), EvidenceDocumentJson.Serialize(RunUnderTest.Evidence("run-1")), token))
            .And("One test process recorded its three tests, one failed, without writing the report", async (run, token) =>
                Assert.True((await RunReport.WriteAsync(run.Options, run.Tests, null, writeReport: false, token)).IsNone))
            .When("Another process with three tests, one failed, writes the run report", async (run, token) =>
                run.Report = (await RunReport.WriteAsync(run.Options, run.Tests, null, writeReport: true, token)).GetValueOrDefault())
            .Then("The headline says two other tests failed", async (run, token) =>
                Assert.Contains("But 2 other tests failed.</p>", await File.ReadAllTextAsync(run.Report!, token)))
            .And("The summary banner shows the run as failed", async (run, token) =>
                Assert.Contains("id=\"summary\" class=\"banner unsatisfied\"", await File.ReadAllTextAsync(run.Report!, token)))
            .VerifyAsync();

    private sealed class BrokenReporter : IAssuranceReporter
    {
        public async Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync("partial"u8.ToArray(), cancellationToken);
            throw new IOException("render failed");
        }
    }

    private sealed class CancelledReporter(OperationCanceledException failure) : IAssuranceReporter
    {
        public Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken) => throw failure;
    }
}

internal sealed class ReportFile : IDisposable
{
    public ReportFile() => HostCancellation = new("stopped", Cancellation.Token);

    public string Folder { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ahr-report-" + Guid.NewGuid().ToString("N"))).FullName;

    public string Path => System.IO.Path.Combine(Folder, "report.html");

    public CancellationTokenSource Cancellation { get; } = new();

    public OperationCanceledException HostCancellation { get; }

    public Task WriteAsync(IAssuranceReporter reporter, AssuranceStatus status, string observation, CancellationToken cancellationToken) =>
        LocalFileArtifactDestination.ForFile(Path).WriteAsync(reporter, new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1),
            [new("scenario", "Scenario", null, [new("Observation", StepKind.Then, new(status, observation), TimeSpan.Zero)])]), []), Path, cancellationToken);

    public void Dispose()
    {
        Cancellation.Dispose();
        Directory.Delete(Folder, recursive: true);
    }
}

internal sealed class ReportFileSteps : ScenarioSteps<ReportFile>
{
    public static ScenarioAction<ReportFile> AnEarlierReport { get; } =
        CreateActionStep("An earlier report is in the folder", (report, token) => File.WriteAllTextAsync(report.Path, "original", token));

    public static ScenarioExpectation<ReportFile> TheEarlierReportIsKept { get; } =
        CreateAssertionStep("The earlier report is kept as it was", async (report, token) =>
            Assert.Equal("original", await File.ReadAllTextAsync(report.Path, token)));

    public static ScenarioExpectation<ReportFile> NothingElseIsLeft { get; } =
        CreateAssertionStep("Nothing else is left in the folder", report =>
            Assert.Equal(["report.html"], Directory.GetFiles(report.Folder).Select(Path.GetFileName)));
}

internal sealed class RunUnderTest : IDisposable
{
    public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ahr-run-" + Guid.NewGuid().ToString("N"))).FullName;

    public ReportOptions Options => new() { DefaultOutputDirectory = Root, CorrelationId = "run-1" };

    public TestRunSummary Tests { get; } = new("", 2, 1, 0, ["A plain test that failed"]);

    public string? Report { get; set; }

    public static VerificationEvidence Evidence(string correlation) =>
        new("v-" + correlation, correlation, "producer", "test", "Test", VerificationExecutionState.Completed,
            new(new("Journey", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
            [new("journey-" + correlation, "A journey", null, [new("Checked", StepKind.Then, new(Satisfied, "Observed"), TimeSpan.Zero)])]), []), []);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
