using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Xunit.Sdk;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class XunitExpectationTests
{
    [Fact]
    public Task AFailedXunitAssertionIsAFailedCheckInTheReport() =>
        Scenario.Define<VerificationUnderTest>("A failed xUnit assertion is a failed check in the report, and the checks after it still run", AdapterBehaviour.Verdicts)
            .Given("A scenario whose awaited Assert.Equal fails, followed by an independent check", verification => verification.Use(Observed(
                ScenarioExpectation<int>.Assertion("Expected amount", async value =>
                {
                    await Task.Yield();
                    Assert.Equal(2, value);
                }, "Compare the observed amount with policy."))
                .And(ScenarioExpectation<int>.Assertion("Independent observation", value =>
                {
                    verification.Ran.Enqueue("Independent observation");
                    Assert.Equal(1, value);
                }))))
            .And(TheBuiltInHtmlReport)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Unsatisfied"))
            .And("The independent check ran", verification => Assert.Contains("Independent observation", verification.Ran))
            .And(TheReportSays("Assert.Equal() Failure"))
            .And(TheReportSays("Compare the observed amount with policy."))
            .And(TheReportDoesNotSay(">Execution error<"))
            .VerifyAsync();

    [Fact]
    public Task AnyOtherExceptionIsAnExecutionErrorWithItsDetailsWithheld()
    {
        Scenario<int> scenario = Observed(ScenarioExpectation<int>.Assertion("Service observation", async (_, _) =>
        {
            await Task.Yield();
            throw new IOException("service unavailable");
        }));
        return Scenario.Define<VerificationUnderTest>("Any other exception in a check is an execution error in the report, with its details withheld",
                AdapterBehaviour.Verdicts,
                description: "An exception's message can hold anything the system had to hand, so the report a stakeholder reads names the error without it.")
            .Given("A scenario whose check fails because a service is unavailable", verification => verification.Use(scenario))
            .And(TheBuiltInHtmlReport)
            .When(ItIsVerified)
            .Then("The run stops with the exception's type", async (_, token) => Assert.Equal(typeof(IOException).FullName,
                (await scenario.RunAsync(token)).Error.ExceptionType))
            .And(TheReportSays("Execution error"))
            .And(TheReportSays("details withheld"))
            .And(TheReportDoesNotSay("service unavailable"))
            .And(TheReportDoesNotSay("xUnit assertion failed:"))
            .VerifyAsync();
    }

    [Fact]
    public Task CancellationReachesTheTestUnchanged()
    {
        OperationCanceledException? canceled = null;
        return Scenario.Define<VerificationUnderTest>("Cancellation reaches the test unchanged, and the report shows the run as canceled", AdapterBehaviour.Cancellation)
            .Given("A scenario whose check is canceled through the token the verification gives it", verification => verification.Use(
                Observed(ScenarioExpectation<int>.Assertion("Canceled", async (_, token) =>
                {
                    Assert.Equal(verification.Cancellation.Token, token);
                    await Task.Yield();
                    throw canceled = new("canceled", token);
                }))))
            .And(TheBuiltInHtmlReport)
            .When("The scenario is verified through the xUnit adapter with that cancellation", verification => verification.VerifyAsync(verification.Cancellation.Token))
            .Then("The test stops with the same cancellation", verification => Assert.Same(canceled, verification.Failure))
            .And(TheReportSays("Execution: Canceled"))
            .And(TheReportSays("Not evaluated"))
            .VerifyAsync();
    }

    [Fact]
    public Task AnAssertionAfterVerificationIsNotReported() =>
        Scenario.Define<VerificationUnderTest>("An assertion made after verification isn't added to the report", XunitBehaviour.Reports)
            .Given("A scenario whose check passes", verification => verification.Use(Observed(ScenarioExpectation<int>.Assertion("Recorded", value => Assert.Equal(1, value)))))
            .And(TheBuiltInHtmlReport)
            .When("The scenario is verified, and an assertion then fails outside it", async (verification, token) =>
            {
                await verification.VerifyAsync(token);
                _ = Assert.Throws<FailException>(() => Assert.Fail("OUTSIDE-VERIFICATION"));
            })
            .Then(TheTestPasses)
            .And(TheReportSays("Recorded"))
            .And(TheReportDoesNotSay("OUTSIDE-VERIFICATION"))
            .VerifyAsync();

    [Fact]
    public Task ChecksInThenAndAndAreAwaited() =>
        Scenario.Define<VerificationUnderTest>("Checks written in a Then and an And are awaited and captured", AdapterBehaviour.Verdicts)
            .Given("A scenario whose Then checks synchronously and whose And awaits before it checks", verification => verification.Use(Unchecked()
                .Then("The value is positive", value => Assert.True(value > 0))
                .And("The value is still one", async value =>
                {
                    await Task.Yield();
                    verification.Ran.Enqueue("The value is still one");
                    Assert.Equal(1, value);
                })))
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("The awaited check ran", verification => Assert.Contains("The value is still one", verification.Ran))
            .And("The last check is recorded with the And keyword", verification =>
                Assert.Equal(StepKeyword.And, verification.Evidence.Report.Run.Scenarios[0].Steps[^1].Keyword))
            .VerifyAsync();

    [Fact]
    public Task TheTestsOutputShowsTheScenarioAndItsEvidence() =>
        Scenario.Define<VerificationUnderTest>("The test's output shows the scenario and where its evidence was written", AdapterBehaviour.Identity)
            .Given("A passing scenario", _ => { })
#pragma warning disable xUnit1051
            .When("It is verified with the project's settings, and the test's output is read", async verification =>
            {
                verification.Result = await Observed(ScenarioExpectation<int>.Assertion("The observed value is one", value => Assert.Equal(1, value))).VerifyAsync();
                verification.Output = TestContext.Current.TestOutputHelper?.Output ?? "";
            })
#pragma warning restore xUnit1051
            .Then("The output shows the scenario's business context, capability and outcome", verification =>
            {
                Assert.Contains("Business assurance", verification.Output);
                Assert.Contains("Business context: Verify observed values against policy", verification.Output);
                Assert.Contains("Capability: Evaluate observed values (evaluate)", verification.Output);
                Assert.Contains("Outcome: The value matches policy (matches)", verification.Output);
            })
            .And("The output shows the scenario's steps", verification =>
            {
                Assert.Contains("Given Ready", verification.Output);
                Assert.Contains("Then The observed value is one", verification.Output);
            })
            .And("The output shows where the JSON evidence was written", verification =>
            {
                AssuranceArtifact json = Assert.Single(verification.Result!.Artifacts);
                Assert.Equal("JSON", json.Name);
                Assert.Contains("Artifact (JSON)", verification.Output);
                Assert.Contains(json.Location, verification.Output);
                Assert.True(File.Exists(json.Location));
            })
            .And("The evidence is in the runs folder that ahr-assurance.json names", verification =>
            {
                var config = Assert.IsType<string>(AppContext.GetData("Ahr.Assurance.ConfigFile"));
                Assert.StartsWith(Path.GetFullPath("artifacts/assurance", Path.GetDirectoryName(config)!) + Path.DirectorySeparatorChar,
                    Path.GetFullPath(Assert.Single(verification.Result!.Artifacts).Location), StringComparison.Ordinal);
            })
            .VerifyAsync();

    [Fact]
    public Task TextOutputCanBeTurnedOffWithoutTurningOffTheReport() =>
        Scenario.Define<VerificationUnderTest>("Text output can be turned off without turning off the report", AdapterBehaviour.Identity)
            .Given("A passing scenario, with text output off", verification =>
                verification.Use(Observed(ScenarioExpectation<int>.Assertion("Recorded", value => Assert.Equal(1, value)))))
            .And(TheBuiltInHtmlReport)
            .When("The scenario is verified through the xUnit adapter, and the test's output is read", async (verification, token) =>
            {
                await verification.VerifyAsync(token);
                verification.Output = TestContext.Current.TestOutputHelper?.Output ?? "";
            })
            .Then(TheTestPasses)
            .And("The test's output is empty", verification => Assert.Empty(verification.Output))
            .And("The report is written", verification => Assert.Single(Directory.GetFiles(verification.Folder)))
            .VerifyAsync();

    private static Scenario<int> Observed(ScenarioExpectation<int> expectation) => Unchecked().Then(expectation);

    private static Scenario<int> Unchecked() =>
        Scenario.Define("Assertion example", _ => Task.FromResult(1), BusinessContext.Define("Verify observed values against policy")
                .Capability("evaluate", "Evaluate observed values")
                .Outcome("matches", "The value matches policy")
                .Build(), id: "id")
            .Given("Ready", _ => { })
            .When("Act", _ => { });
}
