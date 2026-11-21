using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using Xunit.Sdk;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class XunitAdapterConformanceTests
{
    public static TheoryData<string> Cases => [.. AdapterConformance.CaseNames];

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Conforms(string conformanceCase) => AdapterConformance.RunAsync(conformanceCase, new XunitDriver());

    private sealed class XunitDriver : IAdapterConformanceDriver
    {
        public string CapturedOutput => TestContext.Current.TestOutputHelper?.Output ?? "";

        public Task<AssuranceVerificationResult> VerifyAsync<TContext>(
            Scenario<TContext> scenario, ReportOptions options, CancellationToken cancellationToken = default)
            where TContext : notnull => scenario.VerifyWithCancellationAsync(options, cancellationToken);

        public ScenarioExpectation<TContext> Assertion<TContext>(string name, Action<TContext> assertion)
            where TContext : notnull => ScenarioExpectation<TContext>.Assertion(name, assertion);

        public void FailAssertion(string message) => Assert.Fail(message);

        public bool IsNativeFailure(Exception exception) => exception is IAssertionException;

        public void SkipTest(string reason) => Assert.Skip(reason);

        public bool IsNativeSkip(Exception exception) => exception is SkipException;
    }
}
