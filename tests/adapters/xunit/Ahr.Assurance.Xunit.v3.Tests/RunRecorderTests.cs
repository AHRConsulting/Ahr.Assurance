using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.TestingPlatform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class RunRecorderTests
{
    [Fact]
    public Task EveryTestsFinalOutcomeIsCounted() =>
        Scenario.Define<RecordedRun>("The run's report counts each test by its final outcome, and names the tests that didn't pass", XunitBehaviour.Reports)
            .Given("A test that passes after being reported in progress, and tests that fail, error, time out, are skipped or are only discovered",
                async (run, token) =>
                {
                    await run.ReportAsync("a", "Passes", new InProgressTestNodeStateProperty(""), token);
                    await run.ReportAsync("a", "Passes", new PassedTestNodeStateProperty(""), token);
                    await run.ReportAsync("b", "Fails", new FailedTestNodeStateProperty(""), token);
                    await run.ReportAsync("c", "Errors", new ErrorTestNodeStateProperty(""), token);
                    await run.ReportAsync("d", "Times out", new TimeoutTestNodeStateProperty(""), token);
                    await run.ReportAsync("e", "Is skipped", new SkippedTestNodeStateProperty(""), token);
                    await run.ReportAsync("f", "Is only discovered", new DiscoveredTestNodeStateProperty(""), token);
                })
            .When("The run is summarised", run => run.Summary = run.Recorder.Summary())
            .Then("One test passed, three failed and one was skipped", run =>
                Assert.Equal((1, 3, 1), (run.Summary!.Passed, run.Summary.Failed, run.Summary.Skipped)))
            .And("The tests that didn't pass are named", run => Assert.Equal(["Errors", "Fails", "Times out"], run.Summary!.NotPassed))
            .VerifyAsync();

    [Fact]
    public async Task TheCommandLineOptionTakesOnOrOff()
    {
        AssuranceCommandLine options = new();
        Microsoft.Testing.Platform.Extensions.CommandLine.CommandLineOption report =
            options.GetCommandLineOptions().Single(option => option.Name == AssuranceCommandLine.Report);

        Assert.True((await options.ValidateOptionArgumentsAsync(report, ["off"])).IsValid);
        Assert.False((await options.ValidateOptionArgumentsAsync(report, ["maybe"])).IsValid);
    }

    public sealed class RecordedRun
    {
        internal RunRecorder Recorder { get; } = new(null!, () => new ReportOptions());

        internal TestRunSummary? Summary { get; set; }

        internal Task ReportAsync(string uid, string name, TestNodeStateProperty state, CancellationToken cancellationToken) =>
            Recorder.ConsumeAsync(null!, new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
            {
                Uid = new TestNodeUid(uid),
                DisplayName = name,
                Properties = new PropertyBag(state)
            }), cancellationToken);
    }
}
