using System.Collections.Immutable;
using System.Reflection;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Xunit.Abstractions;

namespace Ahr.Assurance.Xunit;

internal static class AssuranceVerification
{
    internal static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ITestOutputHelper output, ReportOptions reportOptions,
        CancellationToken cancellationToken = default) where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(output);
        return VerificationHost.VerifyAsync(name, scenarios, reportOptions, new XunitV2Host(output), cancellationToken);
    }

    private sealed class XunitV2Host(ITestOutputHelper output) : IVerificationHost
    {
        private static readonly Type? _failure = Type.GetType("Xunit.Sdk.XunitException, xunit.assert", throwOnError: false);
        private readonly ITest? _test = output.GetType().GetField("test", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(output) as ITest;

        public CancellationToken CancellationToken => CancellationToken.None;
        public string Producer => Method?.DeclaringType?.Assembly.GetName().Name ?? "verification";

        public Exception CreateFailure(string message, Exception? cause) =>
            _failure?.GetConstructor([typeof(string), typeof(Exception)])?.Invoke([message, cause]) as Exception ?? new ScenarioFailedException(message, cause);

        public ImmutableArray<StepParameter> Example => _test?.TestCase.TestMethodArguments is { Length: > 0 } arguments && Method is { } method
            ? ExampleRow.From(method.GetParameters(), arguments)
            : [];

        public VerificationContext Context => new()
        {
            TestId = _test?.TestCase.UniqueID,
            TestName = _test?.DisplayName,
            PublishTextAsync = (text, token) =>
            {
                token.ThrowIfCancellationRequested();
                output.WriteLine(text);
                return Task.CompletedTask;
            }
        };

        private MethodInfo? Method => (_test?.TestCase.TestMethod.Method as IReflectionMethodInfo)?.MethodInfo;
    }
}

internal sealed class ScenarioFailedException(string message, Exception? cause) : Exception(message, cause);
