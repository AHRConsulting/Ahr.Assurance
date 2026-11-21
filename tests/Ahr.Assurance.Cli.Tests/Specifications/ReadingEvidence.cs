using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli.Tests.Specifications;

internal sealed class ReportCommandRun : IToolRun, IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public ReportFakes.FakeFiles Files { get; } = new();

    public ReportFakes.FakeWriter Writer { get; } = new();

    public string Root => "/";

    public int ExitCode { get; private set; }

    public string Said => _output.ToString() + _error;

    public string Errors => _error.ToString();

    public string Output => _output.ToString();

    public Result<ReportCompletion, CommandError> Result { get; private set; }

    public async Task ReportAsync(ReportRequest request, CancellationToken token)
    {
        _ = _output.GetStringBuilder().Clear();
        _ = _error.GetStringBuilder().Clear();
        ExitCode = await new ReportCommand(new(Files, Writer), _output, _error).RunAsync(request, token);
    }

    public async Task RunApplicationAsync(ReportRequest request, CancellationToken token) =>
        Result = await new ReportApplication(Files, Writer).RunAsync(request, token);

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }
}

public sealed class ReadingEvidence
{
    [Theory]
    [InlineData(null, 1, "ambiguous-run")]
    [InlineData("missing", 1, "unknown-run")]
    [InlineData("one", 0, "Selected run: one")]
    public Task TheReportIsOfExactlyTheRunAskedFor(string? run, int exit, string message) =>
        Scenario.Define<ReportCommandRun>("The report is of exactly the run asked for, and nothing is written when that is unclear", CliBehaviour.ProjectReports)
            .Given("Evidence from two runs, 'one' and 'two'", command =>
            {
                command.Files.Add("a.json", ReportFakes.Evidence("a", "one"));
                command.Files.Add("b.json", ReportFakes.Evidence("b", "two"));
            })
            .When(run is null ? "The report is asked for without naming a run" : $"The report of run '{run}' is asked for", (command, token) =>
                command.ReportAsync(new(["a.json", "b.json"], "output", run, false), token))
            .Then(ReportSteps.TheCliExitsWith(exit))
            .And(ReportSteps.TheCliSays(message))
            .And(exit == 0 ? "The report holds the one verification of that run" : "Nothing is written", command =>
            {
                Assert.Equal(exit == 0 ? 1 : 0, command.Writer.Calls);
                if (command.Writer.Selection is { } selection)
                {
                    Assert.Equal(run, selection.CorrelationId);
                    _ = Assert.Single(selection.Verifications);
                }
            })
            .VerifyAsync();

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public Task EvidenceFromSeveralProjectsKeepsWhereEachCameFrom(bool canceled, int exit) =>
        Scenario.Define<ReportCommandRun>("Evidence from several projects in one run keeps where each came from, and one that was canceled fails the report",
                CliBehaviour.Trust)
            .Given(canceled ? "Two projects' evidence from one run, one of them canceled" : "Two projects' evidence from one run, both completed", command =>
            {
                command.Files.Add("project-a.json", ReportFakes.Evidence("a", "same"));
                command.Files.Add("project-b.json", ReportFakes.Evidence("b", "same") with
                {
                    ExecutionState = canceled ? VerificationExecutionState.Canceled : VerificationExecutionState.Completed
                });
            })
            .When("The report is asked for", (command, token) => command.ReportAsync(new(["project-a.json", "project-b.json"], "output", null, false), token))
            .Then(ReportSteps.TheCliExitsWith(exit))
            .And("Both projects' verifications are reported, in order", command =>
                Assert.Equal(["a", "b"], command.Writer.Selection!.Verifications.Select(item => item.Evidence.VerificationId)))
            .And(ReportSteps.TheCliSays("Suite completion: not established"))
            .VerifyAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AFailedReadOrWriteIsNamedWithItsDetailsOnlyWhenAsked(bool details) =>
        Scenario.Define<ReportCommandRun>("A failed read or write is named, with its details only when asked for", CliBehaviour.Trust,
                description: "An exception's message can hold paths or data, so it is shown only with --details.")
            .Given("An evidence file that can't be read", command => command.Files.Failure = new IOException("SENSITIVE-IO-DETAIL"))
            .When(details ? "The report is asked for with --details" : "The report is asked for", (command, token) =>
                command.ReportAsync(new(["a.json"], "output", null, false, details), token))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And("The read error is named", command => Assert.Contains("evidence-read-error", command.Errors))
            .And(details ? "Its details are shown" : "Its details are not shown", command =>
                Assert.Equal(details, command.Errors.Contains("SENSITIVE-IO-DETAIL", StringComparison.Ordinal)))
            .And("Nothing is written", command => Assert.Equal(0, command.Writer.Calls))
            .When("The file can be read, but the report can't be written", (command, token) =>
            {
                command.Files.Failure = null;
                command.Files.Add("a.json", ReportFakes.Evidence("a", "one"));
                command.Writer.Failure = new("write-error", "Could not write.", "SENSITIVE-WRITE-DETAIL");
                return command.ReportAsync(new(["a.json"], "output", null, false, details), token);
            })
            .Then(ReportSteps.TheCliExitsWith(1))
            .And("The write error is named", command => Assert.Contains("write-error", command.Errors))
            .And(details ? "Its details are shown" : "Its details are not shown", command =>
                Assert.Equal(details, command.Errors.Contains("SENSITIVE-WRITE-DETAIL", StringComparison.Ordinal)))
            .VerifyAsync();

    [Fact]
    public Task AReadThatTimesOutIsAReadFailure() =>
        Scenario.Define<ReportCommandRun>("A read that times out is a failed read, not a cancellation of the report", CliBehaviour.Trust)
            .Given("An evidence file whose read times out", command =>
            {
                command.Files.Failure = new OperationCanceledException("The read timed out.");
                command.Files.Add("a.json", ReportFakes.Evidence("a", "one"));
            })
            .When("The report is built", (command, token) => command.RunApplicationAsync(new(["a.json"], "output", null, false), token))
            .Then("It fails as a read error", command => Assert.Equal("evidence-read-error", command.Result.Match(_ => "", failure => failure.Code)))
            .And("Nothing is written", command => Assert.Equal(0, command.Writer.Calls))
            .VerifyAsync();

    [Fact]
    public Task DuplicateAndMalformedEvidenceIsRefused() =>
        Scenario.Define<ReportCommandRun>("Duplicate or malformed evidence is refused, and nothing is written", CliBehaviour.Trust)
            .Given("Two evidence files for the same verification", command =>
            {
                command.Files.Add("a.json", ReportFakes.Evidence("same", "run"));
                command.Files.Add("b.json", ReportFakes.Evidence("same", "run"));
            })
            .When("The report is built from both", (command, token) => command.RunApplicationAsync(new(["a.json", "b.json"], "output", null, false), token))
            .Then("It fails as duplicate evidence", command => Assert.Equal("duplicate-evidence", command.Result.Match(_ => "", failure => failure.Code)))
            .When("The report is built from a file that isn't valid JSON", (command, token) =>
            {
                command.Files.Json["/invalid.json"] = "{";
                return command.RunApplicationAsync(new(["invalid.json"], "output", null, false), token);
            })
            .Then("It fails as a read error", command => Assert.Equal("evidence-read-error", command.Result.Match(_ => "", failure => failure.Code)))
            .And("Nothing is written", command => Assert.Equal(0, command.Writer.Calls))
            .VerifyAsync();

    [Fact]
    public Task WhenOneOutputCannotBeWrittenTheOthersStillAre() =>
        Scenario.Define<ReportFolder>("When one output can't be written, the others still are, and the report says which failed", CliBehaviour.Trust)
            .Given("A report folder where a verification's HTML page can't be written", folder =>
                _ = Directory.CreateDirectory(Path.Combine(folder.Root, "verifications", "000000", "a.html")))
            .When("The developer report is written", async (folder, token) => folder.Result = await new StandardReportWriter().WriteAsync(
                new("run", [new(ReportFakes.Evidence("a", "run"), folder.Root)], folder.Root, false, Developer: true), token))
            .Then("Writing fails", folder => Assert.False(folder.Result.IsSuccess))
            .And("The verification's evidence is still written", folder =>
                Assert.True(File.Exists(Path.Combine(folder.Root, "verifications", "000000", "a.json"))))
            .And("The report says the page is unavailable, and doesn't link to it", async (folder, token) =>
            {
                var index = await File.ReadAllTextAsync(Path.Combine(folder.Root, "report.html"), token);
                Assert.Contains("HTML unavailable", index);
                Assert.Contains("Delivery error", index);
                Assert.DoesNotContain("href=\"verifications/000000/a.html\"", index);
            })
            .VerifyAsync();
}

internal sealed class ReportFolder : IDisposable
{
    public string Root { get; } = TestReports.TemporaryRoot();

    public Result<string, CommandError> Result { get; set; }

    public void Dispose() => Directory.Delete(Root, true);
}
