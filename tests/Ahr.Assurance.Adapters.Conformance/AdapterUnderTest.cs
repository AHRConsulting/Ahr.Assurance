using System.Collections.Concurrent;
using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Adapters.Conformance;

/// <summary>A scenario verified through an adapter, and what its verification left behind.</summary>
public sealed class AdapterUnderTest(IAdapterConformanceDriver driver) : IDisposable
{
    private Func<CancellationToken, Task<AssuranceVerificationResult>>? _verify;

    public IAdapterConformanceDriver Driver => driver;
    public ConformanceSink Sink { get; set; } = new();
    public AssuranceTextVerbosity TextOutput { get; set; } = AssuranceTextVerbosity.Off;
    public CancellationTokenSource Cancellation { get; } = new();
    public ConcurrentQueue<string> Ran { get; } = new();
    public AssuranceVerificationResult? Result { get; private set; }
    public Exception? Failure { get; private set; }

    /// <summary>The test's output, read in the step that wrote it. NUnit copies a step's output to the test once the step ends.</summary>
    public string Output { get; set; } = "";

    public ReportOptions Options => new() { Sinks = [Sink], TextOutput = TextOutput };
    public VerificationEvidence Evidence => Sink.Single;
    public ImmutableArray<StepResult> Steps => Evidence.Report.Run.Scenarios[0].Steps;

    /// <summary>The scenario's step by its position, counting from 1.</summary>
    public StepResult Step(int number) => Steps[number - 1];

    public void Use<TContext>(Scenario<TContext> scenario)
        where TContext : notnull =>
        _verify = token => driver.VerifyAsync(scenario, Options, token);

    /// <summary>A scenario of Given "Ready", When "Act" and a check made with the adapter's assertion capture.</summary>
    public Scenario<object> Simple(Action<object> check, string name = "A simple scenario") =>
        Scenario.Define(name, () => new object())
            .Given("Ready", _ => { })
            .When("Act", _ => { })
            .Then(driver.Assertion("Check", check));

    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        if (_verify is null)
        {
            throw new InvalidOperationException("No scenario to verify was given.");
        }
        try
        {
            Result = await _verify(cancellationToken);
            Failure = null;
        }
        catch (Exception exception)
        {
            Result = null;
            Failure = exception;
        }
    }

    /// <summary>Fails the current step with the framework's own assertion failure.</summary>
    public void Expect(bool condition, string expectation)
    {
        if (!condition)
        {
            driver.FailAssertion(expectation);
        }
    }

    public void Dispose() => Cancellation.Dispose();
}

/// <summary>Keeps the evidence it is given, and can fail every delivery.</summary>
public sealed class ConformanceSink(Exception? failure = null) : IAssuranceEvidenceSink
{
    private readonly ConcurrentQueue<VerificationEvidence> _evidence = new();
    public string Name => "conformance";
    public IReadOnlyCollection<VerificationEvidence> All => _evidence;
    public VerificationEvidence Single => _evidence.Single();

    public Task<EvidenceDeliveryResult> PublishAsync(
        VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        _evidence.Enqueue(evidence);
        return failure is not null
            ? throw failure
            : Task.FromResult(new EvidenceDeliveryResult([new(Name, "memory://" + evidence.VerificationId)], []));
    }
}
