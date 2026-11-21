using System.Text;
using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class AdapterReporterTests
{
    [Fact]
    public Task ATeamsOwnReportWriterGetsTheReport()
    {
        RecordingReporter reporter = new();
        return Scenario.Define<VerificationUnderTest>("A team's own report writer gets the report, with the reason a verification couldn't run", XunitBehaviour.Reports)
            .Given(NoScenarios("Invalid / journey"))
            .And("The team's own writer writes the report", verification => verification.Sinks.Add(
                new ArtifactReportSink(new("Custom", reporter, new LocalFileArtifactDestination(verification.Folder, ".txt")))))
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("invalid-definition"))
            .And("The writer is given the report, with the invalid definition as its diagnostic", _ =>
                Assert.Equal("invalid-definition", Assert.Single(Assert.IsType<AssuranceReport>(reporter.Report).Diagnostics).Code))
            .And(TheReportSays("Invalid / journey"))
            .VerifyAsync();
    }

    [Fact]
    public Task EveryReportDestinationIsTried()
    {
        RecordingReporter reporter = new();
        RecordingDestination down = new(shouldFail: true);
        RecordingDestination up = new(shouldFail: false);
        return Scenario.Define<VerificationUnderTest>("Every report destination is tried, even when one of them fails", AdapterBehaviour.Delivery)
            .Given(ARenewalWhoseCheckIs(Satisfied))
            .And("Two report destinations, the first of which is down", verification =>
            {
                verification.Sinks.Add(new ArtifactReportSink(new("Primary", reporter, down)));
                verification.Sinks.Add(new ArtifactReportSink(new("Secondary", reporter, up)));
            })
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Primary"))
            .And("Both destinations were tried, and the report was written", _ =>
                Assert.Equal((true, true, true), (down.Attempted, up.Attempted, reporter.Report is not null)))
            .VerifyAsync();
    }

    [Fact]
    public Task ReportsWrittenAtTheSameTimeAreKeptApart() =>
        Scenario.Define<VerificationUnderTest>("Verifications at the same time write separate reports, with names safe for a file", AdapterBehaviour.Isolation)
            .Given("Reports are written to a folder by the team's own writer", verification => verification.Sinks.Add(
                new ArtifactReportSink(new("Custom", new RecordingReporter(), new LocalFileArtifactDestination(verification.Folder, ".txt")))))
            .When("A scenario whose name isn't safe for a file is verified eight times at the same time", verification => Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => Scenario.Define<object>("../../escape/\\:<unsafe>", id: "unsafe")
                    .Given("Ready", _ => { })
                    .When("Act", _ => { })
                    .Then("Observe", _ => new ExpectationResult(Satisfied, "observation"))
                    .VerifyAsync(verification.Options))))
            .Then("There are eight reports, each in the reports folder, with a safe name", verification =>
            {
                var paths = Directory.GetFiles(verification.Folder);
                Assert.Equal(8, paths.Length);
                Assert.All(paths, path =>
                {
                    Assert.Equal(verification.Folder, Path.GetDirectoryName(path));
                    Assert.EndsWith(".txt", path);
                    Assert.DoesNotContain("<", Path.GetFileName(path));
                });
            })
            .VerifyAsync();

    [Fact]
    public Task AnErrorDoesNotHideAFailedCheckOrAFailedReport() =>
        Scenario.Define<VerificationUnderTest>("An error doesn't hide an earlier failed check, or a report that couldn't be written", AdapterBehaviour.Delivery)
            .Given("A scenario whose check fails, followed by a check that throws", verification => verification.Use(Renewal(Unsatisfied)
                .And(new ScenarioExpectation<object>("Unexpected failure", (_, _) => throw new IOException("execution failed")))))
            .And(AReportWriterThatFails("render failed"))
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Unsatisfied"))
            .And(TheTestFailsSaying("execution failed"))
            .And(TheTestFailsSaying("render failed"))
            .And(NoReportIsLeftBehind)
            .VerifyAsync();

    private sealed class RecordingReporter : IAssuranceReporter
    {
        public AssuranceReport? Report { get; private set; }

        public async Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken)
        {
            Report = report;
            await destination.WriteAsync(Encoding.UTF8.GetBytes(report.Run.Name), cancellationToken);
        }
    }

    private sealed class RecordingDestination(bool shouldFail) : IAssuranceArtifactDestination
    {
        public bool Attempted { get; private set; }

        public string ResolveLocation(string artifactName) => $"memory://{artifactName}";

        public async Task WriteAsync(
            IAssuranceReporter reporter,
            AssuranceReport report,
            string location,
            CancellationToken cancellationToken = default)
        {
            Attempted = true;
            if (shouldFail)
            {
                throw new IOException("destination unavailable");
            }

            using MemoryStream stream = new();
            await reporter.WriteAsync(report, stream, cancellationToken);
        }
    }
}
