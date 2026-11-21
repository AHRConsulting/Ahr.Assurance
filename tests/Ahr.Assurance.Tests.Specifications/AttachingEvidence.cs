using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class AttachingEvidence
{
    [Fact]
    public Task AFileIsReferencedWhereItIsAndContentIsWrittenNextToTheEvidence() =>
        Scenario.Define<ScenarioUnderTest>("A file is referenced where it is, and content is written next to the evidence", Capabilities.Attachments,
                description: "A file isn't copied, so a database or a large log costs nothing to attach; it is read wherever it is when the report is opened.")
            .Given("A scenario that attaches a file in its When and a screenshot in its Then", scenario => scenario.Use(Scenario.Define("Attaches a file and a screenshot")
                .Given("Ready", () => { })
                .When("Act", () => Attach.File(Path.Combine(scenario.Output, "shots", "orders.db")))
                .Then("Checked", () => Attach.Bytes("screen", [1, 2, 3], "image/png"))))
            .When(RunSteps.ItIsVerified)
            .And("The file is written after the scenario ran", async (scenario, token) =>
            {
                _ = Directory.CreateDirectory(Path.Combine(scenario.Output, "shots"));
                await File.WriteAllTextAsync(Path.Combine(scenario.Output, "shots", "orders.db"), "database", token);
            })
            .Then("The file is recorded at its step, by its name and where it is", scenario =>
            {
                EvidenceAttachment database = scenario.Evidence.Attachments[0];
                Assert.Equal(("orders.db", "application/octet-stream", 1), (database.Name, database.MediaType, database.StepIndex));
                Assert.Equal("../shots/orders.db", database.Reference);
            })
            .And("The file's current content is what the report opens", async (scenario, token) =>
                Assert.Equal("database", await File.ReadAllTextAsync(scenario.PathOf(scenario.Evidence.Attachments[0]), token)))
            .And("The screenshot is written to the attachments folder at its step", async (scenario, token) =>
            {
                EvidenceAttachment screen = scenario.Evidence.Attachments[1];
                Assert.Equal(2, screen.StepIndex);
                Assert.EndsWith(".png", screen.Reference);
                Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(scenario.PathOf(screen), token));
            })
            .VerifyAsync();

    [Theory]
    [InlineData("reader.log", "text/plain", ".log")]
    [InlineData("response.json", "application/json", ".json")]
    [InlineData("notes", "text/plain", ".txt")]
    public Task TextKeepsItsExtensionAndMediaType(string name, string mediaType, string extension) =>
        Scenario.Define<ScenarioUnderTest>("Attached text keeps its extension and media type", Capabilities.Attachments)
            .Given("A scenario that attaches text named " + name, scenario => scenario.Use(Scenario.Define("Attaches text")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () => Attach.Text(name, "content"))))
            .When(RunSteps.ItIsVerified)
            .Then("Its media type and extension follow its name", scenario =>
            {
                EvidenceAttachment attachment = Assert.Single(scenario.Evidence.Attachments);
                Assert.Equal(mediaType, attachment.MediaType);
                Assert.EndsWith(extension, attachment.Reference);
            })
            .VerifyAsync();

    [Fact]
    public Task AnAttachmentMadeBeforeAFailureIsKept() =>
        Scenario.Define<ScenarioUnderTest>("An attachment made before a failure is kept", Capabilities.Attachments,
                description: "The screenshot of a failure is the one a reader needs most.")
            .Given("A scenario whose check attaches a screenshot and then fails", scenario => scenario.Use(Scenario.Define("Attaches then fails")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () =>
                {
                    Attach.Bytes("screen", [1], "image/png");
                    return ExpectationResult.Check(false, "Failed", "Expected", "Actual");
                })))
            .When(RunSteps.ItIsVerifiedAndFails)
            .Then("The screenshot is written", scenario =>
                Assert.True(File.Exists(scenario.PathOf(Assert.Single(scenario.Evidence.Attachments)))))
            .VerifyAsync();

    [Fact]
    public Task AnOnFailureAttachmentIsDroppedWhenTheScenarioPasses() =>
        Scenario.Define<ScenarioUnderTest>("An attachment kept on failure leaves nothing behind when the scenario passes", Capabilities.Attachments,
                description: "A command's full output is worth reading only when a check on it fails.")
            .Given("A passing scenario that attaches a command's output, kept on failure", scenario => scenario.Use(Scenario.Define("Attaches on failure, then passes")
                .Given("Ready", () => { })
                .When("The command runs", () => Attach.Text("output.txt", "exit code 0", AttachmentKeep.OnFailure))
                .Then("Checked", () => { })))
            .When(RunSteps.ItIsVerified)
            .Then("No attachment is recorded", scenario => Assert.Empty(scenario.Evidence.Attachments))
            .And("No file is written", scenario => Assert.Empty(Directory.EnumerateFiles(scenario.Output, "*.txt", SearchOption.AllDirectories)))
            .VerifyAsync();

    [Theory]
    [InlineData("a check fails")]
    [InlineData("a later step throws")]
    public Task AnOnFailureAttachmentIsKeptInFullWhenTheScenarioFails(string failure) =>
        Scenario.Define<ScenarioUnderTest>("An attachment kept on failure is kept in full, at its step, when the scenario fails", Capabilities.Attachments,
                description: "A failure message may cut long text short; the attachment keeps all of it.")
            .Given($"A scenario that attaches long output, kept on failure, and then {failure}", scenario => scenario.Use(Scenario.Define("Attaches on failure, then fails")
                .Given("Ready", () => { })
                .When("The command runs", () => Attach.Text("output.txt", _longOutput, AttachmentKeep.OnFailure))
                .Then("Checked", () => Fail(failure))))
            .When(RunSteps.ItIsVerifiedAndFails)
            .Then("The output is recorded at the step that produced it", scenario =>
                Assert.Equal(("output.txt", 1), (Assert.Single(scenario.Evidence.Attachments).Name, Assert.Single(scenario.Evidence.Attachments).StepIndex)))
            .And("All of it is written", async (scenario, token) =>
                Assert.Equal(_longOutput, await File.ReadAllTextAsync(scenario.PathOf(scenario.Evidence.Attachments[0]), token)))
            .VerifyAsync();

    private static readonly string _longOutput = string.Concat(Enumerable.Repeat("build: restoring packages for the solution.\n", 200));

    private static void Fail(string failure)
    {
        if (failure == "a check fails")
        {
            Assert.Fail("The output doesn't say 'Commit allowed'.");
        }

        throw new IOException("The repository is locked.");
    }

    [Fact]
    public Task AFileThatDoesNotExistIsStillRecorded() =>
        Scenario.Define<ScenarioUnderTest>("A file that doesn't exist yet is still recorded, without failing the test", Capabilities.Attachments)
            .Given("A scenario that attaches a file nobody has written", scenario => scenario.Use(Scenario.Define("Attaches a missing file")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () => Attach.File(Path.Combine(scenario.Output, "absent.db")))))
            .When(RunSteps.ItIsVerified)
            .Then("The file is recorded where it would be, with no diagnostic", scenario =>
            {
                EvidenceAttachment attachment = Assert.Single(scenario.Evidence.Attachments);
                Assert.Equal(("absent.db", "../absent.db", true), (attachment.Name, attachment.Reference, attachment.Available));
                Assert.Empty(scenario.Evidence.Report.Diagnostics);
            })
            .VerifyAsync();

    [Fact]
    public Task WorkStartedInAStepCanAttach() =>
        Scenario.Define<ScenarioUnderTest>("Work a step starts on another thread can attach to it", Capabilities.Attachments)
            .Given("A scenario whose check attaches from two background tasks", scenario => scenario.Use(Scenario.Define("Attaches from background tasks")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", async () =>
                {
                    await Task.Run(() => Attach.Text("one.txt", "1"));
                    await Task.Run(() => Attach.Text("two.txt", "2"));
                })))
            .When(RunSteps.ItIsVerified)
            .Then("Both are recorded at the check", scenario =>
                Assert.Equal([2, 2], scenario.Evidence.Attachments.Select(attachment => attachment.StepIndex)))
            .VerifyAsync();

    [Fact]
    public Task EachStepKeepsItsOwnAttachments() =>
        Scenario.Define<ScenarioUnderTest>("Each Given, When and Then keeps its own attachments", Capabilities.Attachments)
            .Given("A scenario that attaches in each step", scenario => scenario.Use(Scenario.Define("Attaches in each step")
                .Given("Ready", () => Attach.Text("given.txt", "g"))
                .When("Act", () => Attach.Text("when.txt", "w"))
                .Then("Checked", () => Attach.Text("then.txt", "t"))))
            .When(RunSteps.ItIsVerified)
            .Then("Each attachment is at the step that made it", scenario =>
                Assert.Equal([0, 1, 2], scenario.Evidence.Attachments.Select(attachment => attachment.StepIndex)))
            .And("They all belong to the scenario", scenario => Assert.All(scenario.Evidence.Attachments,
                attachment => Assert.Equal(scenario.Sink.Scenario.Id, attachment.ScenarioId)))
            .VerifyAsync();

    [Fact]
    public Task AttachmentsWhileTheContextIsCreatedOrDisposedGoOnTheScenario() =>
        Scenario.Define<ScenarioUnderTest>("Attachments made while the context is created or disposed belong to the scenario", Capabilities.Attachments)
            .Given("A typed scenario whose context attaches when it is created and when it is disposed", scenario => scenario.Use(
                Scenario.Define("Attaches while created and disposed", () =>
                    {
                        Attach.Text("setup.txt", "setup");
                        return new AttachingContext();
                    })
                    .Given("Ready", _ => { })
                    .When("Act", _ => { })
                    .Then("Checked", _ => { })))
            .When(RunSteps.ItIsVerified)
            .Then("Both are recorded, in order", scenario =>
                Assert.Equal(["setup.txt", "teardown.txt"], scenario.Evidence.Attachments.Select(attachment => attachment.Name)))
            .And("Neither is at a step, and both are on the scenario", scenario => Assert.All(scenario.Evidence.Attachments, attachment =>
                Assert.Equal((null, scenario.Sink.Scenario.Id), (attachment.StepIndex, attachment.ScenarioId))))
            .VerifyAsync();

    [Fact]
    public Task WorkThatOutlivesItsStepAttachesToTheStepRunningThen() =>
        Scenario.Define<ScenarioUnderTest>("Work that outlives its step attaches to the step running when it attaches", Capabilities.Attachments)
            .Given("A scenario whose action starts work that attaches only once the check releases it", scenario =>
            {
                TaskCompletionSource release = new();
                Task? late = null;
                scenario.Use(Scenario.Define("Attaches from outliving work")
                    .Given("Ready", () => { })
                    .When("Act", () =>
                    {
                        late = Task.Run(async () =>
                        {
                            await release.Task;
                            Attach.Text("late.txt", "late");
                        });
                    })
                    .Then("Checked", async () =>
                    {
                        release.SetResult();
                        await late!;
                    }));
            })
            .When(RunSteps.ItIsVerified)
            .Then("The attachment is at the check", scenario =>
                Assert.Equal(2, Assert.Single(scenario.Evidence.Attachments).StepIndex))
            .VerifyAsync();

    [Fact]
    public Task WorkThatOutlivesTheRunCannotAttach() =>
        Scenario.Define<ScenarioUnderTest>("Work that outlives the run can't attach", Capabilities.Attachments,
                description: "The evidence has been published by then, so a late attachment would be lost without anyone knowing.")
            .Given("A scenario whose action starts work that attaches after the run", scenario => scenario.Use(Scenario.Define("Attaches after the run")
                .Given("Ready", () => { })
                .When("Act", () =>
                {
                    scenario.Background = Task.Run(async () =>
                    {
                        await scenario.Release.Task;
                        Attach.Text("late.txt", "late");
                    });
                })
                .Then("Checked", () => { })))
            .When(RunSteps.ItIsVerified)
            .AndThrows<InvalidOperationException>("The work attaches after the run", scenario =>
            {
                scenario.Release.SetResult();
                return scenario.Background!;
            })
            .Then<InvalidOperationException>("It is told it can only attach while a scenario is running", (_, rejection) =>
                Assert.Contains("while a scenario is running", rejection.Message))
            .VerifyAsync();

    [Fact]
    public Task RunningWithoutVerifyingIgnoresAttachments() =>
        Scenario.Define<ScenarioUnderTest>("Running a scenario without verifying it ignores its attachments", Capabilities.Attachments)
            .Given("A scenario that attaches text", scenario => scenario.Use(Scenario.Define("Attaches without verifying")
                .Given("Ready", () => { }).When("Act", () => { })
                .Then("Checked", () => Attach.Text("a.txt", "a"))))
            .When(RunSteps.ItIsRun)
            .Then(RunSteps.TheScenarioIs(Satisfied))
            .VerifyAsync();

    private sealed class AttachingContext : IDisposable
    {
        public void Dispose() => Attach.Text("teardown.txt", "teardown");
    }
}
