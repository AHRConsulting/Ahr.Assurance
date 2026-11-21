using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests.Execution;

public sealed class AttachTests
{
    [Fact]
    public async Task AttachmentsInsideTheResultsFolderMoveWithIt()
    {
        var root = TestReports.TemporaryRoot();
        var moved = TestReports.TemporaryRoot();
        try
        {
            (VerificationEvidence evidence, _) = await Verify(root, Scenario.Define("Moved attachments")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () =>
                {
                    Attach.Text("log.txt", "logged");
                    return Passed();
                }));
            Directory.Delete(moved);
            Directory.Move(root, moved);

            Assert.Equal("logged", await File.ReadAllTextAsync(Resolve(moved, evidence, Assert.Single(evidence.Attachments)), TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            if (Directory.Exists(moved))
            {
                Directory.Delete(moved, true);
            }
        }
    }

    [Fact]
    public async Task ContentThatCannotBeWrittenIsUnavailable()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "blocker"), "", TestContext.Current.CancellationToken);
            (VerificationEvidence evidence, VerificationResult result) = await Verify(root, Scenario.Define("Unwritable attachments")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () =>
                {
                    Attach.Text("log.txt", "logged");
                    return Passed();
                }), "blocker/attachments");

            Assert.True(result.Succeeded);
            Assert.False(Assert.Single(evidence.Attachments).Available);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task NothingIsWrittenWhenThereAreNoSinks()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            _ = await VerificationCoordinator.VerifyAsync("Journey", [Scenario.Define("No sinks")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () =>
                {
                    Attach.Text("log.txt", "logged");
                    return Passed();
                })], new()
                {
                    DefaultOutputDirectory = root,
                    Sinks = [],
                    TextOutput = AssuranceTextVerbosity.Off
                }, new(), TestContext.Current.CancellationToken);

            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Screenshot after: The total is £12.00", "image/png", ".png")]
    [InlineData("checkout.png", "image/png", ".png")]
    [InlineData("checkout.jpeg", "image/jpeg", ".jpeg")]
    [InlineData("report.md", "application/octet-stream", ".md")]
    [InlineData("Version 1.2", "text/plain", ".txt")]
    [InlineData("Trace v1.5", "application/zip", ".zip")]
    public void TheFileExtensionFollowsTheMediaTypeWhenTheNameDisagrees(string name, string mediaType, string extension) =>
        Assert.Equal(extension, AttachmentCapture.Extension(name, mediaType));

    [Fact]
    public async Task EachGivenWhenAndThenKeepsItsOwnAttachments()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            (VerificationEvidence evidence, _) = await Verify(root,
                Scenario.Define("First")
                    .Given("Ready", () => Attach.Text("given.txt", "g"))
                    .When("Act", () => Attach.Text("when.txt", "w"))
                    .Then("Checked", () =>
                    {
                        Attach.Text("then.txt", "t");
                        return Passed();
                    }),
                Scenario.Define("Second")
                    .Given("Ready", () => { }).When("Act", () => { })
                    .Then("Checked", () =>
                    {
                        Attach.Text("second.txt", "s");
                        return Passed();
                    }));

            Assert.Equal([0, 1, 2, 2], evidence.Attachments.Select(attachment => attachment.StepIndex));
            Assert.Equal(3, evidence.Attachments.Count(attachment => attachment.ScenarioId == evidence.Attachments[0].ScenarioId));
            Assert.NotEqual(evidence.Attachments[0].ScenarioId, evidence.Attachments[3].ScenarioId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AttachOutsideARunThrows() =>
        Assert.Contains("while a scenario is running",
            Assert.Throws<InvalidOperationException>(() => Attach.Text("late.txt", "late")).Message);

    private static ExpectationResult Passed() => ExpectationResult.Check(true, "Passed", "Expected", "Actual");

    private static string Resolve(string root, VerificationEvidence evidence, EvidenceAttachment attachment) =>
        Path.GetFullPath(attachment.Reference, Path.GetFullPath(evidence.AttachmentsDirectory!, root));

    private static Task<(VerificationEvidence Evidence, VerificationResult Result)> Verify(string root, params Scenario[] scenarios) =>
        Verify(root, [.. scenarios.Select(scenario => scenario.Definition)]);

    private static Task<(VerificationEvidence Evidence, VerificationResult Result)> Verify(string root, Scenario scenario, string attachmentsDirectory) =>
        Verify(root, [scenario.Definition], attachmentsDirectory);

    private static async Task<(VerificationEvidence Evidence, VerificationResult Result)> Verify<TContext>(string root, Scenario<TContext>[] scenarios,
        string attachmentsDirectory = "attachments")
        where TContext : notnull
    {
        VerificationResult result = await VerificationCoordinator.VerifyAsync("Journey", scenarios, new()
        {
            DefaultOutputDirectory = root,
            AttachmentsDirectory = attachmentsDirectory,
            TextOutput = AssuranceTextVerbosity.Off
        }, new(), TestContext.Current.CancellationToken);
        var json = await File.ReadAllTextAsync(result.Artifacts.Single(artifact => artifact.Name == "JSON").Location, TestContext.Current.CancellationToken);
        return (EvidenceDocumentJson.Read(json).Value, result);
    }
}
