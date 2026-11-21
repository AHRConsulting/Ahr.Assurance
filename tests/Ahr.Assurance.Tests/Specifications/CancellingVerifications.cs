using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.Reporting.VerificationExecutionState;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class CancellingVerifications
{
    [Theory]
    [InlineData("execution", Canceled, NotEvaluated)]
    [InlineData("delivery", Completed, Satisfied)]
    [InlineData("text", Completed, Satisfied)]
    public Task ACancellationIsRethrownAsItWasAndTheEvidenceKept(string phase, VerificationExecutionState state, AssuranceStatus check) =>
        Scenario.Define<VerificationUnderTest>("A cancellation is rethrown as it was, and the evidence so far is kept", EvidenceCapabilities.Cancellation,
                description: "A host matches its own cancellation to stop cleanly, so it must get back the exception it caused.")
            .Given(VerificationSteps.TheHostCancelsDuring(phase))
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsCancelled)
            .Then(VerificationSteps.TheHostsCancellationIsRethrown)
            .And(VerificationSteps.TheExecutionIs(state))
            .And(VerificationSteps.StepIs(1, Satisfied))
            .And(VerificationSteps.StepIs(3, check))
            .And(VerificationSteps.SinksGetATokenThatIsNotCancelled)
            .VerifyAsync();

    [Fact]
    public Task AttachmentsAreFinalisedWhenTheVerificationIsCancelled() =>
        Scenario.Define<VerificationUnderTest>("Attachments are finalised and delivered when the verification is cancelled", EvidenceCapabilities.Cancellation,
                description: "A recording of a cancelled run shows what it was doing when it stopped.")
            .Given("A video is attached as the verification ends", verification => verification.Options = verification.Options with
            {
                FinalizeAttachmentsAsync = token =>
                {
                    verification.Events.Add(token.IsCancellationRequested ? "finalised with a cancelled token" : "finalised");
                    return Task.FromResult<ImmutableArray<EvidenceAttachment>>([new("video", "Video", "video/mp4", "recording.mp4")]);
                }
            })
            .And(VerificationSteps.TheHostCancelsDuring("execution"))
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsCancelled)
            .Then(VerificationSteps.TheHostsCancellationIsRethrown)
            .And(VerificationSteps.TheseRan("given, when, finalised, capture"))
            .And("The video is in the evidence", verification => Assert.Equal("video", Assert.Single(verification.Evidence.Attachments).Id))
            .And(VerificationSteps.TheExecutionIs(Canceled))
            .VerifyAsync();

    [Fact]
    public Task ASinkCancelledDuringDeliveryDoesNotStopTheOthers() =>
        Scenario.Define<VerificationUnderTest>("A sink cancelled during delivery doesn't stop the other sinks", EvidenceCapabilities.Cancellation)
            .Given(VerificationSteps.TheHostCancelsDuring("delivery"))
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsCancelled)
            .Then(VerificationSteps.TheHostsCancellationIsRethrown)
            .And(VerificationSteps.TheseRan("given, when, then, capture"))
            .And(VerificationSteps.TheExecutionIs(Completed))
            .And("The evidence says delivery was cancelled", verification =>
                Assert.Contains("report-delivery-canceled", verification.Evidence.Report.Diagnostics.Select(diagnostic => diagnostic.Code)))
            .VerifyAsync();

    [Fact]
    public Task AVerificationCancelledBeforeItStartsStillDeliversEvidence() =>
        Scenario.Define<VerificationUnderTest>("A verification cancelled before it starts still delivers evidence that says so", EvidenceCapabilities.Cancellation)
            .Given(VerificationSteps.JsonIsWritten)
            .And("The host has already cancelled", verification => verification.Cancellation.Cancel())
            .And(VerificationSteps.AScenarioWhoseCheckIs(Satisfied))
            .When(VerificationSteps.ItIsCancelled)
            .Then(VerificationSteps.TheseRan("capture"))
            .And(VerificationSteps.TheExecutionIs(Canceled))
            .And(VerificationSteps.SinksGetATokenThatIsNotCancelled)
            .And("The JSON evidence says the run was cancelled before any scenario ran", async (verification, token) =>
            {
                VerificationEvidence evidence = await verification.ReadJsonAsync(token);
                Assert.Equal(Canceled, evidence.ExecutionState);
                Assert.Empty(evidence.Report.Run.Scenarios);
            })
            .VerifyAsync();
}
