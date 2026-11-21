using System.Text.Json;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Reporting.VerificationExecutionState;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class WithholdingDetails
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReportsShowOperationalDetailsOnlyWhenAsked(bool included) =>
        Scenario.Define<VerificationUnderTest>("Reports and text show an error's operational details only when asked, and a team's own sinks always get them",
                EvidenceCapabilities.Privacy)
            .Given(included ? VerificationSteps.DetailsAreIncluded : VerificationSteps.DetailsAreWithheld)
            .And("JSON evidence and an HTML report are written", verification => verification.Sinks.AddRange([ReportSinks.Json(), ReportSinks.Html()]))
            .And(VerificationSteps.TextOutputIsOn)
            .And(VerificationSteps.AScenarioWhoseActionFails(VerificationUnderTest.Secret))
            .When(VerificationSteps.ItIsVerified)
            .Then("The test's failure names the error in full", verification =>
                Assert.Contains(verification.Result.Failures, failure => failure.Message.Contains(VerificationUnderTest.Secret, StringComparison.Ordinal)))
            .And("A team's own sink receives the error in full", verification =>
            {
                Assert.Contains(VerificationUnderTest.Secret, verification.Evidence.Report.Diagnostics[0].Message);
                Assert.Contains(VerificationUnderTest.Secret, verification.Step(2).Evaluation.Explanation);
            })
            .And(VerificationSteps.TheFilesShowDetailsOnlyIfIncluded)
            .And(VerificationSteps.TheTextShowsDetailsOnlyIfIncluded)
            .And("The JSON, the HTML and the text say execution stopped", async (verification, token) =>
            {
                foreach (var path in Directory.GetFiles(verification.Output))
                {
                    Assert.Contains("execution-error", await File.ReadAllTextAsync(path, token));
                }
                Assert.Contains("This step stopped execution", verification.Text);
            })
            .And(VerificationSteps.TheExecutionIs(Failed))
            .And("The JSON keeps the execution failure apart from delivery failures", async (verification, token) =>
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(verification.Output, "*.json")), token));
                JsonElement evidence = document.RootElement.GetProperty("evidence");
                _ = Assert.Single(evidence.GetProperty("executionFailures").EnumerateArray());
                Assert.Empty(evidence.GetProperty("deliveryFailures").EnumerateArray());
                Assert.False(evidence.TryGetProperty("artifacts", out _), "The JSON lists the outputs it was delivered to.");
            })
            .VerifyAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AttachmentPathsFollowTheSamePolicy(bool included) =>
        Scenario.Define<VerificationUnderTest>("Missing attachment paths and attachment errors are shown only when asked, while the team's own words always are",
                EvidenceCapabilities.Privacy)
            .Given(included ? VerificationSteps.DetailsAreIncluded : VerificationSteps.DetailsAreWithheld)
            .And("Only JSON evidence and an HTML report are written", verification =>
            {
                verification.Sinks.Clear();
                verification.Sinks.AddRange([ReportSinks.Json(), ReportSinks.Html()]);
            })
            .And("An attachment missing from a private path, and attachments that fail as the verification ends", verification =>
                verification.Options = verification.Options with
                {
                    Attachments = [new("screen", "Screen", "image/png", "/private/" + VerificationUnderTest.Secret)],
                    FinalizeAttachmentsAsync = _ => throw new IOException(VerificationUnderTest.Secret)
                })
            .And(VerificationSteps.TextOutputIsOn)
            .And("A scenario whose check fails in the team's own words", verification => verification.Use(Scenario.Define("A journey with an authored failure")
                .Given("Ready", () => { })
                .When("Act", () => { })
                .Then("Authored evidence remains", () => ExpectationResult.Check(false, "AUTHORED-ASSERTION", "Expected secret", "Actual secret"))))
            .When(VerificationSteps.ItIsVerified)
            .Then("The verification fails", verification => Assert.False(verification.Result.Succeeded, "The verification passed."))
            .And("The JSON marks the attachment unavailable, with its path only when asked", async (verification, token) =>
            {
                EvidenceAttachment attachment = Assert.Single((await verification.ReadJsonAsync(token)).Attachments);
                Assert.False(attachment.Available);
                Assert.Equal(!included, attachment.DetailsWithheld);
                Assert.Equal(included, attachment.Reference.Contains(VerificationUnderTest.Secret, StringComparison.Ordinal));
            })
            .And(VerificationSteps.TheFilesShowDetailsOnlyIfIncluded)
            .And(VerificationSteps.TheTextShowsDetailsOnlyIfIncluded)
            .And("The JSON, the HTML and the text keep the team's own words", async (verification, token) =>
            {
                foreach (var path in Directory.GetFiles(verification.Output))
                {
                    var content = await File.ReadAllTextAsync(path, token);
                    Assert.Contains("AUTHORED-ASSERTION", content);
                    Assert.Contains("Expected secret", content);
                }
                Assert.Contains("AUTHORED-ASSERTION", verification.Text);
            })
            .VerifyAsync();

    [Fact]
    public Task SavedEvidenceRenderedAgainWithholdsDetails() =>
        Scenario.Define<VerificationUnderTest>("Saved evidence rendered again withholds operational details by default", EvidenceCapabilities.Privacy,
                description: "Evidence kept with its details, by a team's own sink, is still safe to render into a report to share.")
            .Given(VerificationSteps.AScenarioWhoseActionFails(VerificationUnderTest.Secret))
            .When(VerificationSteps.ItIsVerified)
            .And("A team's own sink saves its evidence, with the details", verification =>
                Assert.Contains(VerificationUnderTest.Secret, EvidenceDocumentJson.Serialize(verification.Evidence)))
            .And("The saved evidence is rendered as JSON and HTML", async (verification, token) =>
                verification.Rendered = await EvidencePublisher.PublishAsync(EvidenceDocumentJson.Read(EvidenceDocumentJson.Serialize(verification.Evidence)).Value,
                    [ReportSinks.Json(), ReportSinks.Html()], new(verification.Output), token))
            .Then("Both are written", verification =>
            {
                Assert.Empty(verification.Rendered!.Diagnostics);
                Assert.Equal(2, Directory.GetFiles(verification.Output).Length);
            })
            .And(VerificationSteps.TheFilesShowDetailsOnlyIfIncluded)
            .And("Both say the details were withheld", async (verification, token) =>
            {
                foreach (var path in Directory.GetFiles(verification.Output))
                {
                    Assert.Contains("This step stopped execution; operational details withheld.", await File.ReadAllTextAsync(path, token));
                }
            })
            .VerifyAsync();
}
