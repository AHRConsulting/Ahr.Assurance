using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Tests.Specifications;

internal sealed class ScenarioUnderTest : IDisposable
{
    private string? _output;
    private Func<CancellationToken, Task<Result<AssuranceRunResult, AssuranceExecutionError>>>? _run;
    private Func<ReportOptions, Task<AssuranceVerificationResult>>? _verify;

    public List<string> Calls { get; } = [];

    public CancellationTokenSource Cancellation { get; } = new();

    public Task? Background { get; set; }

    public TaskCompletionSource Release { get; } = new();

    public Result<AssuranceRunResult, AssuranceExecutionError>? Outcome { get; set; }

    public CapturingSink Sink { get; } = new();

    public string Output => _output ??= Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ahr-specification-" + Guid.NewGuid().ToString("N"))).FullName;

    public VerificationEvidence Evidence => Sink.Single;

    public string PathOf(EvidenceAttachment attachment) =>
        Path.GetFullPath(attachment.Reference, Sink.Context.AttachmentsDirectory!);

    public List<IAssuranceEvidenceSink> Sinks { get; } = [];

    public AssuranceRunResult Run => Assert.NotNull(Outcome).Value;

    public AssuranceExecutionError Error => Assert.NotNull(Outcome).Error;

    public ScenarioResult Result => Assert.Single(Run.Scenarios);

    public AssuranceRunResult PartialRun => Assert.IsType<AssuranceRunResult>(Error.PartialRun);

    public bool Stopped => Assert.NotNull(Outcome).IsFailure;

    public ScenarioResult Recorded => Stopped ? Assert.Single(PartialRun.Scenarios) : Result;

    public StepResult Step(int number) => Result.Steps[number - 1];

    public void Use(Scenario scenario)
    {
        _run = scenario.RunAsync;
        _verify = options => InlineScenarioExtensions.VerifyAsync(scenario, options);
    }

    public void Use<TContext>(Scenario<TContext> scenario) where TContext : notnull
    {
        _run = token => scenario.RunAsync(token);
        _verify = options => ScenarioVerificationExtensions.VerifyAsync(scenario, options);
    }

    public void Dispose()
    {
        Cancellation.Dispose();
        if (_output is not null && Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
    }

    internal Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync(CancellationToken cancellationToken) =>
        (_run ?? throw new InvalidOperationException("No scenario was written.")).Invoke(cancellationToken);

    internal Task<AssuranceVerificationResult> VerifyAsync() =>
        (_verify ?? throw new InvalidOperationException("No scenario was written.")).Invoke(new ReportOptions
        {
            DefaultOutputDirectory = Output,
            Sinks = [Sink, .. Sinks],
            TextOutput = AssuranceTextVerbosity.Off
        });
}

internal sealed class CapturingSink : IAssuranceEvidenceSink
{
    private readonly List<VerificationEvidence> _evidence = [];

    public string Name => "capture";

    public VerificationEvidence Single => Assert.Single(_evidence);

    public EvidenceDeliveryContext Context { get; private set; } = new(".");

    public ScenarioResult Scenario => Assert.Single(Single.Report.Run.Scenarios);

    public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        _evidence.Add(evidence);
        Context = context;
        return Task.FromResult(EvidenceDeliveryResult.Empty);
    }
}

internal sealed class RunSteps : ScenarioSteps<ScenarioUnderTest>
{
    public static ScenarioAction<ScenarioUnderTest> ItIsRun { get; } =
        CreateActionStep("The scenario is run", async (scenario, token) => scenario.Outcome = await scenario.RunAsync(token));

    public static ScenarioAction<ScenarioUnderTest> ItIsVerified { get; } =
        CreateActionStep("The scenario is verified", scenario => scenario.VerifyAsync());

    public static ScenarioAction<ScenarioUnderTest> ItIsVerifiedAndFails { get; } =
        CreateThrowsStep<global::Xunit.Sdk.XunitException>("The scenario is verified", scenario => scenario.VerifyAsync());

    public static ScenarioExpectation<ScenarioUnderTest> TheScenarioIs(AssuranceStatus status) =>
        CreateAssertionStep($"The scenario is {status}", scenario => Assert.Equal(status, scenario.Run.Status));

    public static ScenarioExpectation<ScenarioUnderTest> StepIs(int number, AssuranceStatus status) =>
        CreateAssertionStep($"Step {number} is {status}", scenario =>
            Assert.Equal(status, scenario.Step(number).Evaluation.Status));

    public static ScenarioExpectation<ScenarioUnderTest> TheRunStopsWithAnError(string message) =>
        CreateAssertionStep($"The run stops with an error saying '{message}'", scenario =>
            Assert.Contains(message, scenario.Error.Message));

    public static ScenarioExpectation<ScenarioUnderTest> TheStepsRanInTheOrder(string order) =>
        CreateAssertionStep($"The steps ran in this order: {order}", scenario =>
            Assert.Equal(order, string.Join(", ", scenario.Calls)));

    public static ScenarioExpectation<ScenarioUnderTest> StepIsNamed(int number, string name) =>
        CreateAssertionStep($"Step {number} is named '{name}'", scenario =>
            Assert.Equal(name, scenario.Step(number).Name));

    public static ScenarioExpectation<ScenarioUnderTest> StepRecords(int number, string values) =>
        CreateAssertionStep($"Step {number} records {values}", scenario =>
            Assert.Equal(values, Values(scenario.Step(number).Parameters)));

    internal static string Values(IEnumerable<StepParameter> parameters) =>
        string.Join(", ", parameters.Select(parameter => $"{parameter.Name} = {parameter.Value}"));
}
