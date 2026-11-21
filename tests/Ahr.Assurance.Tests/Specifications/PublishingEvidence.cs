using System.Collections.Immutable;
using System.Text;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.Reporting.VerificationExecutionState;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class PublishingEvidence
{
    [Fact]
    public Task EverySinkReceivesTheSameEvidence() =>
        Scenario.Define<VerificationUnderTest>("Every sink receives the same evidence, and a broken sink doesn't stop the others", EvidenceCapabilities.Delivery)
            .Given("A broken sink, then another working sink", verification =>
            {
                verification.Sinks.Add(new FailingSink("broken", new IOException("sink failed")));
                verification.Sinks.Add(new RecordingSink("last", verification.Events));
            })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then("Both working sinks receive the same evidence of a completed run", verification =>
            {
                RecordingSink last = Assert.IsType<RecordingSink>(verification.Sinks[^1]);
                Assert.Same(verification.Evidence, last.Evidence);
                Assert.Equal(Completed, verification.Evidence.ExecutionState);
            })
            .And(VerificationSteps.TheVerificationFailsWith("report-sink-error"))
            .And(VerificationSteps.TheOutputsListed("capture, last"))
            .VerifyAsync();

    [Fact]
    public Task WithNoSinksTheScenarioStillCounts() =>
        Scenario.Define<VerificationUnderTest>("With no sinks, the scenario still runs and its verdict still counts", EvidenceCapabilities.Delivery)
            .Given("No sinks", verification => verification.Sinks.Clear())
            .And(VerificationSteps.AScenarioWhoseCheckIs(Unsatisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheseRan("given, when, then"))
            .And("The verdict is Unsatisfied", verification => Assert.Equal(Unsatisfied, verification.Result.Status))
            .And("The verification fails", verification => Assert.False(verification.Result.Succeeded, "An unsatisfied scenario passed."))
            .VerifyAsync();

    [Fact]
    public Task JsonEvidenceReadsBackWithItsIdentity() =>
        Scenario.Define<VerificationUnderTest>("JSON evidence reads back with the test and run it came from", EvidenceCapabilities.Delivery,
                description: "Each test writes its own file, with no shared manifest, so tests can run in parallel and in separate processes.")
            .Given(VerificationSteps.JsonIsWritten)
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then("The JSON file is listed with the outputs", verification =>
                Assert.Single(verification.Result.Artifacts, artifact => artifact.Name == "JSON"))
            .And("It reads back with the run, the test, the scenario and how the run ended", async (verification, token) =>
            {
                VerificationEvidence evidence = await verification.ReadJsonAsync(token);
                Assert.Equal(("correlation", "test-id", Completed), (evidence.CorrelationId, evidence.TestId, evidence.ExecutionState));
                Assert.Equal("A delivered journey", Assert.Single(evidence.Report.Run.Scenarios).Name);
            })
            .And("No manifest is written", verification =>
                Assert.Empty(Directory.GetFiles(verification.Output, "manifest.json", SearchOption.AllDirectories)))
            .VerifyAsync();

    [Fact]
    public Task AFailingApplicationAndAFailingSinkAreReportedSeparately() =>
        Scenario.Define<VerificationUnderTest>("A failing application and a failing sink are reported separately", EvidenceCapabilities.Delivery)
            .Given("A broken sink", verification => verification.Sinks.Insert(0, new FailingSink("broken", new IOException("sink failed"))))
            .And(VerificationSteps.AScenarioWhoseActionFails("application failed"))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheExecutionIs(Failed))
            .And(VerificationSteps.TheVerificationFailsWith("execution-error"))
            .And(VerificationSteps.TheVerificationFailsWith("report-sink-error"))
            .VerifyAsync();

    [Fact]
    public Task AttachmentsMoveWithTheirFolder() =>
        Scenario.Define<VerificationUnderTest>("Attachments move with their folder, and a report rendered from there still shows them", EvidenceCapabilities.Delivery,
                description: "Evidence is often uploaded from CI and opened somewhere else.")
            .Given(VerificationSteps.JsonIsWritten)
            .And("A screenshot is attached as the verification ends", verification => verification.Options = verification.Options with
            {
                AttachmentSource = new Screenshots(),
                FinalizeAttachmentsAsync = _ => Task.FromResult<ImmutableArray<EvidenceAttachment>>([new("screen", "<Screen>", "image/png", "memory:screen")])
            })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .And("The evidence folder is moved", verification =>
            {
                verification.Moved = verification.TemporaryFolder();
                Directory.Delete(verification.Moved);
                Directory.Move(verification.Output, verification.Moved);
            })
            .And("An HTML report is rendered from the moved evidence", async (verification, token) =>
                verification.Rendered = await EvidencePublisher.PublishAsync(await verification.ReadJsonAsync(token, verification.Moved), [new HtmlReportSink()],
                    new(verification.TemporaryFolder()) { AttachmentSource = new AttachmentFolderSource(Path.Combine(verification.Moved!, "attachments")) }, token))
            .Then("The screenshot was copied into the evidence's attachments folder", async (verification, token) =>
            {
                VerificationEvidence evidence = await verification.ReadJsonAsync(token, verification.Moved);
                Assert.Equal("attachments", evidence.AttachmentsDirectory);
                Assert.Equal($"{evidence.VerificationId}-screen.png", Assert.Single(evidence.Attachments).Reference);
            })
            .And("The report shows it from its own attachments folder", async (verification, token) =>
            {
                Assert.Empty(verification.Rendered!.Diagnostics);
                var page = Assert.Single(verification.Rendered.Artifacts).Location;
                var html = await File.ReadAllTextAsync(page, token);
                var reference = $"attachments/{(await verification.ReadJsonAsync(token, verification.Moved)).VerificationId}-screen.png";
                Assert.Contains("&lt;Screen&gt;", html);
                Assert.Contains($"<img src=\"{reference}\"", html);
                Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(page)!, reference)), $"{reference} wasn't copied next to the report.");
            })
            .VerifyAsync();

    [Fact]
    public Task AMissingAttachmentDoesNotStopTheEvidence() =>
        Scenario.Define<VerificationUnderTest>("A missing attachment is reported, and the JSON evidence is still written", EvidenceCapabilities.Delivery)
            .Given(VerificationSteps.JsonIsWritten)
            .And("An attachment whose file is missing", verification => verification.Options = verification.Options with
            {
                Attachments = [new("missing", "Missing", "image/png", "missing.png")]
            })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheVerificationFailsWith("report-attachment-delivery-error"))
            .And("The JSON evidence still lists the attachment, and says it couldn't be delivered", async (verification, token) =>
            {
                VerificationEvidence evidence = await verification.ReadJsonAsync(token);
                Assert.Equal("missing", Assert.Single(evidence.Attachments).Id);
                Assert.Contains("report-attachment-delivery-error", evidence.Report.Diagnostics.Select(diagnostic => diagnostic.Code));
            })
            .VerifyAsync();

    [Fact]
    public Task AnExplicitCorrelationIdIsRecordedWithItsSource() =>
        Scenario.Define<VerificationUnderTest>("A correlation ID given in the options is recorded, with where it came from", EvidenceCapabilities.Delivery)
            .Given("The options give the correlation ID 'explicit-run'", verification =>
                verification.Options = verification.Options with { CorrelationId = "explicit-run" })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then("The evidence records 'explicit-run', from the options", verification =>
                Assert.Equal(("explicit-run", "options"), (verification.Evidence.CorrelationId, verification.Evidence.CorrelationSource)))
            .VerifyAsync();

    [Fact]
    public Task SinksWithTheSameNameAreRejectedBeforeAnythingRuns() =>
        Scenario.Define<VerificationUnderTest>("Two sinks with the same name are rejected before anything runs", EvidenceCapabilities.Delivery)
            .Given("A second sink also named 'capture'", verification => verification.Sinks.Add(new RecordingSink("capture", verification.Events)))
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .WhenThrows<ArgumentException>("The scenario is verified", verification => verification.VerifyAsync())
            .Then("Nothing ran", verification => Assert.Empty(verification.Events))
            .VerifyAsync();

    [Fact]
    public Task EachSinkKeepsItsOwnSettings() =>
        Scenario.Define<VerificationUnderTest>("Each sink keeps its own folder and format settings", EvidenceCapabilities.Delivery)
            .Given("An HTML report in its own folder, and JSON written on one line", verification =>
            {
                verification.Sinks.Add(new HtmlReportSink(new() { OutputDirectory = Path.Combine(verification.Output, "html"), MaxTextLength = 64 }));
                verification.Sinks.Add(new JsonReportSink(new() { WriteIndented = false }));
            })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then("The HTML report is in its own folder", verification =>
                Assert.StartsWith(Path.Combine(verification.Output, "html"), Assert.Single(verification.Result.Artifacts, artifact => artifact.Name == "HTML").Location))
            .And("The JSON is in the output folder, on one line", async (verification, token) =>
                Assert.DoesNotContain('\n', await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(verification.Output, "*.json")), token)))
            .VerifyAsync();

    [Fact]
    public Task WithoutATextOutputTheVerificationFailsButStillDelivers() =>
        Scenario.Define<VerificationUnderTest>("Without anywhere to write its text, the verification fails but still runs and delivers", EvidenceCapabilities.Delivery,
                description: "Text that can't be written is a failure a team sees, not output silently lost.")
            .Given(VerificationSteps.TextOutputIsOn)
            .And("The host has nowhere to write text", verification => verification.Host = verification.Host with { PublishTextAsync = null })
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheseRan("given, when, then, capture"))
            .And(VerificationSteps.TheVerificationFailsWith("report-text-error"))
            .And(VerificationSteps.TheOutputsListed("capture"))
            .VerifyAsync();

    [Fact]
    public Task EvidenceAndTextArePublishedBeforeTheVerificationReturns() =>
        Scenario.Define<VerificationUnderTest>("Evidence is delivered and the text published before the verification returns", EvidenceCapabilities.Delivery)
            .Given(VerificationSteps.TextOutputIsOn)
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheseRan("given, when, then, capture, text"))
            .And("The evidence names the test and the Assurance version that produced it", verification =>
            {
                Assert.Equal(("test-id", "Host test"), (verification.Evidence.TestId, verification.Evidence.TestName));
                Assert.Equal(AssuranceVersion.Current, verification.Evidence.AssuranceVersion);
                Assert.DoesNotContain('+', AssuranceVersion.Current);
            })
            .And("The text shows each step and where the evidence went", verification =>
            {
                Assert.Contains("Given", verification.Text);
                Assert.Contains("When", verification.Text);
                Assert.Contains("Then", verification.Text);
                Assert.Contains("memory://capture", verification.Text);
            })
            .VerifyAsync();

    [Fact]
    public Task FailedOutputsDoNotEraseWhatTheRunSaw() =>
        Scenario.Define<VerificationUnderTest>("A failed sink or text output doesn't erase what the run saw", EvidenceCapabilities.Delivery)
            .Given("A broken sink before the working one", verification => verification.Sinks.Insert(0, new FailingSink("failed", new IOException("persistence broken"))))
            .And("Text output that fails", verification =>
            {
                verification.Options = verification.Options with { TextOutput = AssuranceTextVerbosity.Standard };
                verification.Host = verification.Host with { PublishTextAsync = (_, _) => throw new IOException("text broken") };
            })
            .And("A scenario whose check is unsatisfied and whose next step breaks", verification => verification.Use(Scenario.Define("A journey that breaks after its check")
                .Given("Ready", () => verification.Events.Add("given"))
                .When("Work", () => verification.Events.Add("when"))
                .Then("Checked", () =>
                {
                    verification.Events.Add("then");
                    return new ExpectationResult(Unsatisfied, "Observed verdict");
                })
                .And("Application error", () => throw new IOException("execution broken"))))
            .When(VerificationSteps.ItIsVerified)
            .Then(VerificationSteps.TheVerificationFailsWith("report-sink-error"))
            .And(VerificationSteps.TheVerificationFailsWith("report-text-error"))
            .And("The failures name the application's error and the unsatisfied check", verification =>
            {
                Assert.Contains(verification.Result.Failures, failure => failure.Message.Contains("execution broken", StringComparison.Ordinal));
                Assert.Contains(verification.Result.Failures, failure => failure.Message.Contains("Unsatisfied", StringComparison.Ordinal));
            })
            .And(VerificationSteps.TheExecutionIs(Failed))
            .And(VerificationSteps.StepIs(3, Unsatisfied))
            .And(VerificationSteps.StepIs(4, NotEvaluated))
            .And(VerificationSteps.TheOutputsListed("capture"))
            .VerifyAsync();

    private sealed class Screenshots : IAttachmentContentSource
    {
        public Task<Stream> OpenReadAsync(EvidenceAttachment attachment, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("sample content")));
    }
}
