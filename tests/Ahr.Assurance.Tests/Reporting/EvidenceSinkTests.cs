using System.Collections.Immutable;
using System.Diagnostics;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests;

public sealed class EvidenceSinkTests
{
    [Fact]
    public async Task FolderSourceResolvesReferencesOutsideTheFolder()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            _ = Directory.CreateDirectory(Path.Combine(root, "attachments"));
            await File.WriteAllTextAsync(Path.Combine(root, "outside.txt"), "outside", TestContext.Current.CancellationToken);
            await using Stream stream = await new AttachmentFolderSource(Path.Combine(root, "attachments"))
                .OpenReadAsync(new("id", "file", "text/plain", "../outside.txt"), TestContext.Current.CancellationToken);
            using StreamReader reader = new(stream);
            Assert.Equal("outside", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DefaultOptionsAllowReplacementAndTextDisableInAnyInitializerOrder()
    {
        ReportOptions options = new()
        {
            TextOutput = AssuranceTextVerbosity.Off,
            Sinks = []
        };
        Assert.Empty(options.Sinks);
        IAssuranceEvidenceSink defaultSink = Assert.Single(new ReportOptions().Sinks);
        Assert.Equal("JSON", defaultSink.Name);
        VerificationResult result = await VerificationCoordinator.VerifyAsync("Default options", [Journey()], options,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(result.Artifacts);
        Assert.Empty(TestContext.Current.TestOutputHelper?.Output ?? "");
    }

    [Fact]
    public async Task ThreeThousandParallelVerificationsWriteIndependentJsonWithoutManifest()
    {
        var root = TestReports.TemporaryRoot();
        var elapsed = Stopwatch.StartNew();
        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, 3000), new ParallelOptions
            {
                MaxDegreeOfParallelism = 8,
                CancellationToken = TestContext.Current.CancellationToken
            }, async (_, _) =>
            {
                VerificationResult result = await Verify(Options([new JsonReportSink()]) with
                {
                    DefaultOutputDirectory = root
                });
                Assert.True(result.Succeeded);
            });
            var files = Directory.GetFiles(root, "*.json");
            Assert.Equal(3000, files.Length);
            Assert.Empty(Directory.GetFiles(root, "manifest.json", SearchOption.AllDirectories));
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"3000 verifications: {elapsed.Elapsed}; JSON bytes: {files.Sum(path => new FileInfo(path).Length)}; files: {files.Length}");
        }
        finally { Directory.Delete(root, true); }
    }

    private static Scenario Journey() => Scenario.Define("A journey")
        .Given("Ready", () => { }).When("Work", () => { }).Then("Checked", () => new ExpectationResult(AssuranceStatus.Satisfied, "Confirmed"));

    private static ReportOptions Options(ImmutableArray<IAssuranceEvidenceSink> sinks) =>
        new()
        {
            Sinks = sinks,
            CorrelationId = "correlation",
            TextOutput = AssuranceTextVerbosity.Off
        };

    private static Task<VerificationResult> Verify(ReportOptions options) =>
        VerificationCoordinator.VerifyAsync("A verification", [Journey()], options,
            new()
            {
                TestId = "test-id",
                TestName = "Host test"
            }, TestContext.Current.CancellationToken);
}
