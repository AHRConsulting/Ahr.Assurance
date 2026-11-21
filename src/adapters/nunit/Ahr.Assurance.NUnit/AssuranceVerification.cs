using System.Collections.Immutable;
using System.Reflection;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Ahr.Assurance.NUnit;

internal static class AssuranceVerification
{
    internal static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions reportOptions,
        CancellationToken cancellationToken = default) where TContext : notnull =>
        VerificationHost.VerifyAsync(name, scenarios, reportOptions, new NUnitHost(TestContext.CurrentContext), cancellationToken);

    private sealed class NUnitHost(TestContext current) : IVerificationHost
    {
        private static readonly PropertyInfo? _cancellationToken = typeof(TestContext).GetProperty("CancellationToken", typeof(CancellationToken));
        private readonly TestExecutionContext _test = TestExecutionContext.CurrentContext;
        private readonly IMethodInfo? _method = TestExecutionContext.CurrentContext.CurrentTest?.Method;

        public CancellationToken CancellationToken => _cancellationToken?.GetValue(current) is CancellationToken token ? token : CancellationToken.None;
        public string Producer => _method?.TypeInfo.Assembly.GetName().Name ?? "verification";
        public Exception CreateFailure(string message, Exception? cause) => new AssertionException(message, cause);
        public Task RunStepAsync(Func<Task> step) => NUnitStepIsolation.RunAsync(_test, step);
        public ImmutableArray<StepParameter> Example => current.Test is { Arguments.Length: > 0 } && _method is not null
            ? ExampleRow.From(_method.MethodInfo.GetParameters(), current.Test.Arguments)
            : [];
        public VerificationContext Context => new()
        {
            TestId = current.Test.ID,
            TestName = current.Test.FullName,
            PublishTextAsync = (text, token) =>
            {
                token.ThrowIfCancellationRequested();
                TestContext.Out.WriteLine(text);
                return Task.CompletedTask;
            }
        };
    }
}
