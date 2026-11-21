using System.Collections.Immutable;
using System.Reflection;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.MSTest;

internal static class AssuranceVerification
{
#if !MSTEST3
    private static readonly PropertyInfo? _current = typeof(TestContext).GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
#endif

    internal static Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions reportOptions, TestContext? testContext,
        CancellationToken cancellationToken = default) where TContext : notnull =>
        VerificationHost.VerifyAsync(name, scenarios, reportOptions, new MSTestHost(testContext ?? Current()), cancellationToken);

#if MSTEST3
    private static TestContext Current() =>
        throw new InvalidOperationException("MSTest 3 has no current test context. Pass the test's TestContext: .VerifyAsync(TestContext).");
#else
    private static TestContext Current() =>
        _current.ToOption()
            .ToResult(new Error("MSTest before 4.5 has no current test context. Pass the test's TestContext: .VerifyAsync(TestContext)."))
            .Bind(property => (property.GetValue(null) as TestContext).ToOption()
                .ToResult(new Error("MSTest did not provide a current test. Call VerifyAsync from inside a test method.")))
            .OrThrow(static error => new InvalidOperationException(error.Message));
#endif

    private sealed class MSTestHost(TestContext current) : IVerificationHost
    {
        private static readonly PropertyInfo? _testData = typeof(TestContext).GetProperty("TestData", typeof(object[]));
        private static readonly PropertyInfo? _testDisplayName = typeof(TestContext).GetProperty("TestDisplayName", typeof(string));
        private readonly object?[]? _data = _testData?.GetValue(current) as object?[];
        private readonly Option<MethodInfo> _method = TestMethod(current);

#if MSTEST3
        public CancellationToken CancellationToken => current.CancellationTokenSource.Token;
#else
        public CancellationToken CancellationToken => current.CancellationToken;
#endif
        public string Producer => _method.Bind(method => method.DeclaringType.ToOption()).Bind(type => type.Assembly.GetName().Name.ToOption()).GetValueOrDefault("verification");
        public Exception CreateFailure(string message, Exception? cause) => cause is null ? new AssertFailedException(message) : new AssertFailedException(message, cause);
        public ImmutableArray<StepParameter> Example =>
            _data is { Length: > 0 } data ? _method.Map(method => ExampleRow.From(method.GetParameters(), data)).GetValueOrDefault([]) : [];
        public VerificationContext Context => new()
        {
            TestId = $"{current.FullyQualifiedTestClassName}.{_testDisplayName?.GetValue(current) as string ?? current.TestName}",
            TestName = $"{current.FullyQualifiedTestClassName}.{current.TestName}",
            PublishTextAsync = (text, token) =>
            {
                token.ThrowIfCancellationRequested();
                current.WriteLine(text);
                return Task.CompletedTask;
            }
        };

        private static Option<MethodInfo> TestMethod(TestContext context)
        {
            var values = (_testData?.GetValue(context) as object?[])?.Length;
            return from className in context.FullyQualifiedTestClassName.ToOption()
                   from methodName in context.TestName.ToOption()
                   from method in AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(className, throwOnError: false))
                       .OfType<Type>()
                       .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                       .FirstOrNone(method => method.Name == methodName && (values is null || method.GetParameters().Length == values))
                   select method;
        }
    }
}
