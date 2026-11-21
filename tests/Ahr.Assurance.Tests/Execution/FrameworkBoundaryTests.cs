using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests;

public sealed class FrameworkBoundaryTests
{
    [Fact]
    public async Task SharedHostUsesFrameworkTokenAndPublishesBeforeNativeFailure()
    {
        using FakeHost host = new();
        Scenario<object> scenario = Scenario.Define("Portable journey", () => new object())
            .Given("Ready", _ => { })
            .When("Act", (_, token) => { Assert.Equal(host.CancellationToken, token); return Task.CompletedTask; })
            .Then(ScenarioExpectation<object>.Assertion("Check", _ => Assert.Fail("broken check")));
#pragma warning disable xUnit1051
        FakeFailure failure = await Assert.ThrowsAsync<FakeFailure>(() =>
            VerificationHost.VerifyAsync("Portable", [scenario], new()
            {
                Sinks = []
            }, host));
#pragma warning restore xUnit1051
        Assert.True(host.TextPublished);
        Assert.Contains("xUnit assertion failed", failure.Message);
        Assert.Contains("Unsatisfied", failure.Message);
    }

    [Fact]
    public async Task AStepsExceptionReachesTheFailureWithItsTypeAndStack()
    {
        using FakeHost host = new();
        Scenario<object> scenario = Scenario.Define("Breaking journey", () => new object())
            .Given("Ready", _ => { })
            .When("Act", _ => Break())
            .Then(ScenarioExpectation<object>.Assertion("Check", _ => { }));
#pragma warning disable xUnit1051
        FakeFailure failure = await Assert.ThrowsAsync<FakeFailure>(() =>
            VerificationHost.VerifyAsync("Breaking", [scenario], new()
            {
                Sinks = []
            }, host));
#pragma warning restore xUnit1051
        Assert.Contains("[execution-error] System.InvalidOperationException: boom", failure.Message);
        InvalidOperationException cause = Assert.IsType<InvalidOperationException>(failure.InnerException);
        _ = Assert.IsType<IOException>(cause.InnerException);
        Assert.Contains(nameof(Break), cause.StackTrace);
    }

    [Fact]
    public async Task AFailedChecksStackLeadsTheFailuresStack()
    {
        using FakeHost host = new();
        Scenario<object> scenario = Scenario.Define("Failing journey", () => new object())
            .When("Act", _ => { })
            .Then(ScenarioExpectation<object>.Assertion("Check", _ => FailTheCheck()))
            .And(ScenarioExpectation<object>.Assertion("Check again", _ => Assert.Fail("second")));
#pragma warning disable xUnit1051
        FakeFailure failure = await Assert.ThrowsAsync<FakeFailure>(() =>
            VerificationHost.VerifyAsync("Failing", [scenario], new()
            {
                Sinks = []
            }, host));
#pragma warning restore xUnit1051
        Assert.Null(failure.InnerException);
        Assert.Contains(nameof(FailTheCheck), StackBeforeAssurance(failure));
    }

    [Fact]
    public async Task AStepsExceptionStackLeadsTheFailuresStack()
    {
        using FakeHost host = new();
        Scenario<object> scenario = Scenario.Define("A journey that breaks at once", () => new object())
            .When("Act", _ => Break())
            .Then(ScenarioExpectation<object>.Assertion("Check", _ => { }));
#pragma warning disable xUnit1051
        FakeFailure failure = await Assert.ThrowsAsync<FakeFailure>(() =>
            VerificationHost.VerifyAsync("Breaking", [scenario], new()
            {
                Sinks = []
            }, host));
#pragma warning restore xUnit1051
        Assert.Contains(nameof(Break), StackBeforeAssurance(failure));
    }

    private static string StackBeforeAssurance(Exception failure)
    {
        var stack = failure.StackTrace ?? "";
        var host = stack.IndexOf("VerificationHost", StringComparison.Ordinal);
        return host < 0 ? stack : stack[..host];
    }

    private static void FailTheCheck() => Assert.Fail("first");

    private static void Break() => throw new InvalidOperationException("boom", new IOException("inner"));

    private sealed class FakeFailure(string message, Exception? cause) : Exception(message, cause);

    private sealed class FakeHost : IVerificationHost, IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        public CancellationToken CancellationToken => _source.Token;
        public void Dispose() => _source.Dispose();
        public string Producer => "fake-host";
        public bool TextPublished { get; private set; }
        public Exception CreateFailure(string message, Exception? cause)
        {
            Assert.True(TextPublished);
            return new FakeFailure(message, cause);
        }
        public VerificationContext Context => new()
        {
            PublishTextAsync = (_, _) => { TextPublished = true; return Task.CompletedTask; }
        };
    }
}
