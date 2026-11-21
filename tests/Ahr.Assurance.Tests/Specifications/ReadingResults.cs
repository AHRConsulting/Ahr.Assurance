using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class ReadingResults
{
    [Fact]
    public Task AFolderLoadsItsOwnEvidenceInNameOrder() =>
        Scenario.Define<ResultsFolder>("A folder loads the evidence files in it, in name order", EvidenceCapabilities.Results)
            .Given(ResultsSteps.EvidenceIsSavedAs("b.json", "second"))
            .And(ResultsSteps.EvidenceIsSavedAs("a.json", "first"))
            .And(ResultsSteps.EvidenceIsSavedAs("nested/c.json", "nested"))
            .And("A note that isn't evidence", folder => File.WriteAllText(Path.Combine(folder.Root, "notes.txt"), "not evidence"))
            .When(ResultsSteps.TheFolderIsLoaded)
            .Then(ResultsSteps.TheVerificationsAre("first, second"))
            .And(ResultsSteps.TheyBelongToTheFolder)
            .VerifyAsync();

    [Fact]
    public Task OneFileLoadsOnItsOwn() =>
        Scenario.Define<ResultsFolder>("One evidence file loads on its own, and belongs to its folder", EvidenceCapabilities.Results)
            .Given(ResultsSteps.EvidenceIsSavedAs("a.json", "first"))
            .And(ResultsSteps.EvidenceIsSavedAs("b.json", "second"))
            .When("b.json is loaded on its own", (folder, token) => folder.LoadAsync(Path.Combine(folder.Root, "b.json"), null, token))
            .Then(ResultsSteps.TheVerificationsAre("second"))
            .And(ResultsSteps.TheyBelongToTheFolder)
            .VerifyAsync();

    [Fact]
    public Task AttachmentsAreFoundInTheRecordedFolder() =>
        Scenario.Define<ResultsFolder>("Attachments are found in the folder the evidence recorded", EvidenceCapabilities.Results)
            .Given(ResultsSteps.EvidenceIsSavedAs("a.json", "first"))
            .When(ResultsSteps.TheFolderIsLoaded)
            .Then("The screenshot is in the recorded attachments folder", folder =>
            {
                VerificationEvidence evidence = folder.Evidence;
                Assert.Equal(Path.Combine(folder.Root, "attachments"), folder.Results.GetAttachmentsDirectory(evidence));
                Assert.Equal(Path.Combine(folder.Root, "attachments", "screen.png"), folder.Results.GetAttachmentPath(evidence, evidence.Attachments[0]));
            })
            .And("An attachment that was never written has no path", folder =>
                Assert.Null(folder.Results.GetAttachmentPath(folder.Evidence, folder.Evidence.Attachments[1])))
            .VerifyAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AnAttachmentsFolderGivenWhenLoadingIsUsed(bool absolute) =>
        Scenario.Define<ResultsFolder>("An attachments folder given when loading is used instead of the recorded one", EvidenceCapabilities.Results,
                description: "Attachments uploaded apart from their evidence can still be found.")
            .Given(ResultsSteps.EvidenceIsSavedAs("a.json", "first"))
            .When("The folder is loaded with the attachments folder " + (absolute ? "'uploaded' inside it, by its full path" : "'uploaded', relative to where the CLI runs"), (folder, token) =>
                folder.LoadAsync(folder.Root, absolute ? Path.Combine(folder.Root, "uploaded") : "uploaded", token))
            .Then("The screenshot is in the folder given", folder =>
            {
                var given = Path.GetFullPath(absolute ? Path.Combine(folder.Root, "uploaded") : "uploaded");
                Assert.Equal(given, folder.Results.GetAttachmentsDirectory(folder.Evidence));
                Assert.Equal(Path.Combine(given, "screen.png"), folder.Results.GetAttachmentPath(folder.Evidence, folder.Evidence.Attachments[0]));
            })
            .VerifyAsync();

    [Fact]
    public Task InvalidEvidenceNamesItsFile() =>
        Scenario.Define<ResultsFolder>("Invalid evidence names the file it is in", EvidenceCapabilities.Results)
            .Given("A JSON file that isn't evidence", folder => File.WriteAllText(Path.Combine(folder.Root, "broken.json"), "{}"))
            .WhenThrows<InvalidDataException>("The folder is loaded", (folder, token) => folder.LoadAsync(folder.Root, null, token))
            .Then<InvalidDataException>("The error names the file", (folder, error) =>
                Assert.StartsWith($"{Path.Combine(folder.Root, "broken.json")} is not valid evidence: ", error.Message))
            .VerifyAsync();

    [Fact]
    public Task AMissingFolderIsAnError() =>
        Scenario.Define<ResultsFolder>("Loading a folder that doesn't exist is an error", EvidenceCapabilities.Results)
            .Given("No folder named 'missing'", folder => Assert.False(Directory.Exists(Path.Combine(folder.Root, "missing"))))
            .WhenThrows<DirectoryNotFoundException>("'missing' is loaded", (folder, token) => folder.LoadAsync(Path.Combine(folder.Root, "missing"), null, token))
            .Then<DirectoryNotFoundException>("The error names the folder", (folder, error) => Assert.Contains("missing", error.Message))
            .VerifyAsync();
}

/// <summary>A folder of evidence files, and the results loaded from it.</summary>
public sealed class ResultsFolder : IDisposable
{
    public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ahr-results-" + Guid.NewGuid().ToString("N"))).FullName;

    public AssuranceResults Results
    {
        get => field ?? throw new InvalidOperationException("No results were loaded.");
        private set;
    }

    public VerificationEvidence Evidence => Assert.Single(Results.Verifications);

    public void Save(string name, string id)
    {
        var path = Path.Combine(Root, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, EvidenceDocumentJson.Serialize(new(id, "run", "producer", "test", "Test", VerificationExecutionState.Completed,
            new(new("Journey", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
                [new("journey", "Journey", null, [new("Satisfied", StepKind.Then, new(AssuranceStatus.Satisfied, "Observed"), TimeSpan.Zero)])]), []),
            [new("screen", "Screen", "image/png", "screen.png", "journey", 0), new("log", "Log", "text/plain", AttachmentCapture.MissingReference, Available: false)])
        {
            AttachmentsDirectory = "attachments"
        }));
    }

    public async Task LoadAsync(string path, string? attachments, CancellationToken cancellationToken) =>
        Results = await AssuranceResults.LoadAsync(path, attachments, cancellationToken);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

public sealed class ResultsSteps : ScenarioSteps<ResultsFolder>
{
    public static ScenarioAction<ResultsFolder> EvidenceIsSavedAs(string name, string id) =>
        CreateActionStep($"Evidence is saved as {name}, for verification '{id}'", folder => folder.Save(name, id));

    public static ScenarioAction<ResultsFolder> TheFolderIsLoaded { get; } =
        CreateActionStep("The folder is loaded", (folder, token) => folder.LoadAsync(folder.Root, null, token));

    public static ScenarioExpectation<ResultsFolder> TheVerificationsAre(string ids) =>
        CreateAssertionStep($"The verifications loaded are: {ids}", folder =>
            Assert.Equal(ids, string.Join(", ", folder.Results.Verifications.Select(verification => verification.VerificationId))));

    public static ScenarioExpectation<ResultsFolder> TheyBelongToTheFolder { get; } =
        CreateAssertionStep("They belong to the folder", folder => Assert.Equal(folder.Root, folder.Results.Directory));
}
