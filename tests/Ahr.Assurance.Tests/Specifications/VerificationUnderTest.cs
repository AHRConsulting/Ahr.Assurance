using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using static Ahr.Assurance.Core.AssuranceStatus;

namespace Ahr.Assurance.Tests.Specifications;

internal sealed class VerificationUnderTest : IDisposable
{
    public const string Secret = "SECRET-OPERATIONAL-PATH";

    private readonly List<string> _folders = [];
    private Func<ReportOptions, CancellationToken, Task<VerificationResult>>? _verify;

    public VerificationUnderTest()
    {
        Capture = new("capture", Events);
        Sinks = [Capture];
        Host = new()
        {
            TestId = "test-id",
            TestName = "Host test",
            PublishTextAsync = (text, _) =>
            {
                Events.Add("text");
                Text = text;
                return Task.CompletedTask;
            }
        };
    }

    public List<string> Events { get; } = [];

    public RecordingSink Capture { get; }

    public List<IAssuranceEvidenceSink> Sinks { get; }

    public ReportOptions Options { get; set; } = new() { TextOutput = AssuranceTextVerbosity.Off, CorrelationId = "correlation" };

    public VerificationContext Host { get; set; }

    public string? Text { get; private set; }

    public CancellationTokenSource Cancellation { get; } = new();

    public OperationCanceledException HostCancellation { get; } = new("host canceled");

    public string? CancelIn { get; private set; }

    public Exception? ActionFailure { get; set; }

    public AssuranceStatus Verdict { get; set; } = Satisfied;

    public string? Moved { get; set; }

    public EvidenceDeliveryResult? Rendered { get; set; }

    public string Output => field ??= TemporaryFolder();

    public VerificationResult Result
    {
        get => field ?? throw new InvalidOperationException("The verification didn't return a result.");
        private set;
    }

    public VerificationEvidence Evidence => Capture.Evidence ?? throw new InvalidOperationException("No evidence reached the capturing sink.");

    public StepResult Step(int number) => Assert.Single(Evidence.Report.Run.Scenarios).Steps[number - 1];

    public string TemporaryFolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ahr-evidence-" + Guid.NewGuid().ToString("N"))).FullName;
        _folders.Add(folder);
        return folder;
    }

    public async Task<VerificationEvidence> ReadJsonAsync(CancellationToken cancellationToken, string? folder = null) =>
        EvidenceDocumentJson.Read(await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(folder ?? Output, "*.json")), cancellationToken)).Value;

    public void Use(Scenario scenario) =>
        _verify = (options, token) => VerificationCoordinator.VerifyAsync("A verification", [scenario], options, Host, token);

    public void UseJourney() => Use(Scenario.Define("A delivered journey")
        .Given("Ready", () => Events.Add("given"))
        .When("Work", () =>
        {
            Events.Add("when");
            if (CancelIn == "execution")
            {
                throw HostCancellation;
            }
            if (ActionFailure is not null)
            {
                throw ActionFailure;
            }
        })
        .Then("Checked", () =>
        {
            Events.Add("then");
            return new ExpectationResult(Verdict, "Observed verdict");
        }));

    public void CancelDuring(string phase)
    {
        CancelIn = phase;
        if (phase == "delivery")
        {
            Sinks.Insert(0, new FailingSink("cancelling", HostCancellation));
        }
        else if (phase == "text")
        {
            Options = Options with { TextOutput = AssuranceTextVerbosity.Standard };
            Host = Host with { PublishTextAsync = (_, _) => throw HostCancellation };
        }
    }

    internal async Task VerifyAsync() =>
        Result = await (_verify ?? throw new InvalidOperationException("No scenario was written.")).Invoke(
            Options with { DefaultOutputDirectory = Output, Sinks = [.. Sinks] }, Cancellation.Token);

    public void Dispose()
    {
        Cancellation.Dispose();
        foreach (var folder in _folders.Where(Directory.Exists))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

internal sealed class RecordingSink(string name, List<string> events) : IAssuranceEvidenceSink
{
    public string Name => name;

    public VerificationEvidence? Evidence { get; private set; }

    public CancellationToken Token { get; private set; }

    public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
    {
        events.Add(name);
        Evidence = evidence;
        Token = cancellationToken;
        return Task.FromResult(new EvidenceDeliveryResult([new(Name, "memory://" + Name)], []));
    }
}

internal sealed class FailingSink(string name, Exception failure) : IAssuranceEvidenceSink
{
    public string Name => name;

    public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default) =>
        throw failure;
}

internal sealed class VerificationSteps : ScenarioSteps<VerificationUnderTest>
{
    public static ScenarioAction<VerificationUnderTest> AScenarioWhoseCheckIs(AssuranceStatus verdict) =>
        CreateActionStep($"A scenario whose check is {verdict}", verification =>
        {
            verification.Verdict = verdict;
            verification.UseJourney();
        });

    public static ScenarioAction<VerificationUnderTest> AScenarioWhoseActionFails(string message) =>
        CreateActionStep($"A scenario whose action fails with '{message}'", verification =>
        {
            verification.ActionFailure = new IOException(message);
            verification.UseJourney();
        });

    public static ScenarioAction<VerificationUnderTest> TheHostCancelsDuring(string phase) =>
        CreateActionStep($"The host cancels during {phase}", verification => verification.CancelDuring(phase));

    public static ScenarioAction<VerificationUnderTest> JsonIsWritten { get; } =
        CreateActionStep("Evidence is also written as JSON", verification => verification.Sinks.Add(ReportSinks.Json()));

    public static ScenarioAction<VerificationUnderTest> TextOutputIsOn { get; } =
        CreateActionStep("Text output is on", verification =>
            verification.Options = verification.Options with { TextOutput = AssuranceTextVerbosity.Standard });

    public static ScenarioAction<VerificationUnderTest> DetailsAreIncluded { get; } =
        CreateActionStep("Operational details are included", verification =>
            verification.Options = verification.Options with { IncludeDiagnosticDetails = true });

    public static ScenarioAction<VerificationUnderTest> DetailsAreWithheld { get; } =
        CreateActionStep("Operational details are withheld, as by default", verification =>
            verification.Options = verification.Options with { IncludeDiagnosticDetails = false });

    public static ScenarioAction<VerificationUnderTest> ItIsVerified { get; } =
        CreateActionStep("The scenario is verified", verification => verification.VerifyAsync());

    public static ScenarioAction<VerificationUnderTest> ItIsCancelled { get; } =
        CreateThrowsStep<OperationCanceledException>("The verification is cancelled", verification => verification.VerifyAsync());

    public static ScenarioExpectation<VerificationUnderTest> TheHostsCancellationIsRethrown { get; } =
        CreateThrownStep<OperationCanceledException>("The host's own cancellation is rethrown", (verification, cancellation) =>
            Assert.Same(verification.HostCancellation, cancellation));

    public static ScenarioExpectation<VerificationUnderTest> TheseRan(string order) =>
        CreateAssertionStep($"These ran, in order: {order}", verification =>
            Assert.Equal(order, string.Join(", ", verification.Events)));

    public static ScenarioExpectation<VerificationUnderTest> TheVerificationFailsWith(string code) =>
        CreateAssertionStep($"The verification fails with {code}", verification =>
            Assert.Contains(code, verification.Result.Failures.Select(failure => failure.Code)));

    public static ScenarioExpectation<VerificationUnderTest> TheExecutionIs(VerificationExecutionState state) =>
        CreateAssertionStep($"The evidence records the execution as {state}", verification =>
            Assert.Equal(state, verification.Evidence.ExecutionState));

    public static ScenarioExpectation<VerificationUnderTest> StepIs(int number, AssuranceStatus status) =>
        CreateAssertionStep($"Step {number} is {status} in the evidence", verification =>
            Assert.Equal(status, verification.Step(number).Evaluation.Status));

    public static ScenarioExpectation<VerificationUnderTest> TheOutputsListed(string names) =>
        CreateAssertionStep($"The outputs listed are: {names}", verification =>
            Assert.Equal(names, string.Join(", ", verification.Result.Artifacts.Select(artifact => artifact.Name))));

    public static ScenarioExpectation<VerificationUnderTest> SinksGetATokenThatIsNotCancelled { get; } =
        CreateAssertionStep("Sinks are given a token that isn't cancelled", verification =>
            Assert.False(verification.Capture.Token.IsCancellationRequested, "The sink was given a cancelled token."));

    public static ScenarioExpectation<VerificationUnderTest> TheFilesShowDetailsOnlyIfIncluded { get; } =
        CreateAssertionStep("The JSON and HTML show the operational details only if they are included", async (verification, token) =>
        {
            foreach (var path in Directory.GetFiles(verification.Output))
            {
                var content = await File.ReadAllTextAsync(path, token);
                Assert.True(verification.Options.IncludeDiagnosticDetails == content.Contains(VerificationUnderTest.Secret, StringComparison.Ordinal),
                    $"{Path.GetFileName(path)} {(verification.Options.IncludeDiagnosticDetails ? "leaves out" : "shows")} the operational details.");
            }
        });

    public static ScenarioExpectation<VerificationUnderTest> TheTextShowsDetailsOnlyIfIncluded { get; } =
        CreateAssertionStep("The text shows the operational details only if they are included", verification =>
            Assert.True(verification.Options.IncludeDiagnosticDetails == verification.Text!.Contains(VerificationUnderTest.Secret, StringComparison.Ordinal),
                $"The text {(verification.Options.IncludeDiagnosticDetails ? "leaves out" : "shows")} the operational details:\n{verification.Text}"));
}
