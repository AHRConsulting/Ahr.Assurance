using System.Collections.Immutable;
using System.Reflection;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using TUnit.Assertions.Exceptions;

namespace Ahr.Assurance.TUnit;

internal static class AssuranceVerification
{
    internal static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions reportOptions,
        CancellationToken cancellationToken = default) where TContext : notnull =>
        VerificationHost.VerifyAsync(name, scenarios, reportOptions, new TUnitHost(TestContext.Current
            ?? throw new InvalidOperationException("TUnit did not provide a current test. Call VerifyAsync from inside a test method.")), cancellationToken);

    private sealed class TUnitHost(TestContext current) : IVerificationHost
    {
        private readonly TestDetails _details = current.Metadata.TestDetails;

        public CancellationToken CancellationToken => current.Execution.CancellationToken;
        public string Producer => _details.ClassType.Assembly.GetName().Name ?? "verification";
        public Exception CreateFailure(string message, Exception? cause) => cause is null ? new AssertionException(message) : new AssertionException(message, cause);
        public ImmutableArray<StepParameter> Example => _details.TestMethodArguments is { Length: > 0 } arguments && TestMethod() is { } method
            ? ExampleRow.From(method.GetParameters(), arguments)
            : [];
        public VerificationContext Context => new()
        {
            TestId = _details.TestId,
            TestName = $"{_details.ClassType.FullName}.{current.Metadata.DisplayName}",
            PublishTextAsync = (text, token) =>
            {
                token.ThrowIfCancellationRequested();
                current.Output.WriteLine(text);
                return Task.CompletedTask;
            }
        };

        private MethodInfo? TestMethod() => _details.ClassType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == _details.MethodName && method.GetParameters().Length == _details.TestMethodArguments.Length);
    }
}
