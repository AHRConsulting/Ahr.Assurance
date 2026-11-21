using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Ahr.Assurance.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

[MemoryDiagnoser]
public class AssuranceBenchmarks
{
    private static readonly ReportOptions _noOutput = new() { Sinks = [], TextOutput = AssuranceTextVerbosity.Off };
    private static readonly BusinessContext _business = BusinessContext.Define("benchmark", "Benchmark", "Benchmark context")
        .Capability("capability", "Capability").Outcome("outcome", "Outcome").Risk("risk", "Risk").Build();

    private readonly StakeholderReportWriter _html = new();
    private Scenario<object> _scenario = null!;
    private VerificationEvidence _evidence = null!;
    private AssuranceReport _largeReport = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scenario = Define();
        _evidence = new("verification", "correlation", "producer", "test", "Test",
            VerificationExecutionState.Completed, Report(1), []);
        _largeReport = Report(1_000);
    }

    [Benchmark]
    public Scenario<object> DefineScenario() => Define();

    [Benchmark]
    public Task RunScenario() => _scenario.RunAsync();

    [Benchmark(Description = "Verify, no sinks or text")]
    public Task VerifyWithoutOutput() =>
        VerificationCoordinator.VerifyAsync("Benchmark", [_scenario], _noOutput, new VerificationContext());

    [Benchmark(Description = "Serialize one evidence file")]
    public string SerializeEvidence() => EvidenceDocumentJson.Serialize(_evidence, writeIndented: false);

    [Benchmark(Description = "Render HTML, one scenario")]
    public string RenderHtml() => _html.Render(_evidence.Report, "Benchmark");

    [Benchmark(Description = "Render HTML, 1000 scenarios")]
    public string RenderLargeHtml() => _html.Render(_largeReport, "Benchmark");

    private static Scenario<object> Define() =>
        Scenario.Define("Benchmark journey", () => new object(), business: _business)
            .Given("Ready", _ => { })
            .When("Act", _ => { })
            .Then("Check", _ => new ExpectationResult(AssuranceStatus.Satisfied, "Confirmed", "expected", "expected"))
            .And("Second check", _ => new ExpectationResult(AssuranceStatus.Satisfied, "Confirmed"));

    private static AssuranceReport Report(int scenarios) => new(new("Benchmark", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [.. Enumerable.Range(0, scenarios).Select(index => new ScenarioResult($"scenario-{index}", $"Scenario {index}", _business,
        [
            new("Ready", StepKind.Given, new(AssuranceStatus.Satisfied, "Step completed."), TimeSpan.Zero),
            new("Act", StepKind.When, new(AssuranceStatus.Satisfied, "Step completed."), TimeSpan.Zero),
            new("Check", StepKind.Then, new(AssuranceStatus.Satisfied, "Confirmed", "expected", "expected"), TimeSpan.Zero),
        ]))]), [], []);
}
