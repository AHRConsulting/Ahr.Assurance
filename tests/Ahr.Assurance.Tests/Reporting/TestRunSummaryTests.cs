using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests;

public sealed class TestRunSummaryTests
{
    [Fact]
    public async Task SummariesAreReadForTheirOwnRunAndCombined()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            await new TestRunSummary("run-1", 3, 1, 0, ["Broken test"]).WriteAsync(root, token);
            await new TestRunSummary("run-1", 2, 0, 1, []).WriteAsync(root, token);
            await new TestRunSummary("run-2", 9, 9, 9, ["Another run"]).WriteAsync(root, token);

            TestRunSummary combined = TestRunSummary.Read(root, ["run-1"]).Value;

            Assert.Equal((5, 1, 1, 7), (combined.Passed, combined.Failed, combined.Skipped, combined.Total));
            Assert.Equal(["Broken test"], combined.NotPassed);
            Assert.Equal("7 tests ran: 5 passed, 1 failed, 1 skipped. 2 recorded scenario evidence.", combined.Describe(2));
            Assert.True(TestRunSummary.Read(root, ["run-3"]).IsNone);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AMissingOrUnreadableSummaryIsNone()
    {
        var root = TestReports.TemporaryRoot();
        try
        {
            Assert.True(TestRunSummary.Read(root, ["run-1"]).IsNone);
            _ = Directory.CreateDirectory(Path.Combine(root, TestRunSummary.Folder));
            await File.WriteAllTextAsync(Path.Combine(root, TestRunSummary.Folder, "tests-broken.json"), "{ not json", TestContext.Current.CancellationToken);
            Assert.True(TestRunSummary.Read(root, ["run-1"]).IsNone);
        }
        finally { Directory.Delete(root, true); }
    }
}
