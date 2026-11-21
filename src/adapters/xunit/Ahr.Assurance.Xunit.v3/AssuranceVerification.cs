using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Ahr.Assurance.Xunit;

internal static class AssuranceVerification
{
    internal static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions reportOptions,
        CancellationToken cancellationToken = default) where TContext : notnull =>
        VerificationHost.VerifyAsync(name, scenarios, reportOptions, new XunitHost(TestContext.Current), cancellationToken);

    private sealed class XunitHost(ITestContext current) : IVerificationHost
    {
        public CancellationToken CancellationToken => current.CancellationToken;
        public string Producer => current.TestAssembly?.AssemblyName?.Split(',')[0] ?? "verification";
        public Exception CreateFailure(string message, Exception? cause) => new XunitException(message, cause);
        public ImmutableArray<StepParameter> Example => current.Test is IXunitTest { TestMethodArguments.Length: > 0 } test
            ? ExampleRow.From(test.TestMethod.Method.GetParameters(), test.TestMethodArguments)
            : [];
        public VerificationContext Context => new()
        {
            TestId = current.Test?.UniqueID,
            TestName = current.Test?.TestDisplayName,
            PublishTextAsync = (text, token) =>
            {
                token.ThrowIfCancellationRequested();
                ITestOutputHelper output = current.TestOutputHelper ??
                    throw new InvalidOperationException("xUnit v3 did not provide a current-test output helper.");
                output.WriteLine(text);
                return Task.CompletedTask;
            }
        };
    }
}
