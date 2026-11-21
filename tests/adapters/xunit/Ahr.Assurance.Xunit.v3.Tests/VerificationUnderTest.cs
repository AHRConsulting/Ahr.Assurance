using System.Collections.Concurrent;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Xunit.Sdk;

namespace Ahr.Assurance.XunitV3.Tests;

/// <summary>A verification through the xUnit adapter, the reports it was given, and what it left behind.</summary>
public sealed class VerificationUnderTest : IDisposable
{
    private readonly CapturingSink _capture = new();
    private string? _folder;
    private Func<ReportOptions, CancellationToken, Task<AssuranceVerificationResult>>? _verify;

    /// <summary>A folder of its own for the verification's reports, removed with the context.</summary>
    public string Folder => _folder ??= Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ahr-xunit-" + Guid.NewGuid().ToString("N"))).FullName;

    public string ReportPath => Path.Combine(Folder, "report.html");

    /// <summary>Where the verification's reports go, besides the evidence the context keeps.</summary>
    public List<IAssuranceEvidenceSink> Sinks { get; } = [];

    public AssuranceTextVerbosity TextOutput { get; set; } = AssuranceTextVerbosity.Off;

    public CancellationTokenSource Cancellation { get; } = new();

    public ConcurrentQueue<string> Ran { get; } = new();

    public AssuranceVerificationResult? Result { get; set; }

    public Exception? Failure { get; private set; }

    /// <summary>The test's output, read in the step that verified.</summary>
    public string Output { get; set; } = "";

    public ReportOptions Options => new() { DefaultOutputDirectory = Folder, Sinks = [_capture, .. Sinks], TextOutput = TextOutput };

    public XunitException TestFailure => Assert.IsAssignableFrom<XunitException>(Failure);

    public VerificationEvidence Evidence => Assert.Single(_capture.Evidence);

    /// <summary>The one report written to the folder.</summary>
    public string Report => File.ReadAllText(Assert.Single(Directory.GetFiles(Folder)));

    public void Use<TContext>(Scenario<TContext> scenario) where TContext : notnull =>
        _verify = scenario.VerifyWithCancellationAsync;

    public void Use(Scenario scenario) => _verify = (options, _) => scenario.VerifyAsync(options);

    public void Use<TContext>(string name, IEnumerable<Scenario<TContext>> scenarios) where TContext : notnull =>
        _verify = (options, token) => AssuranceAssert.VerifyWithCancellationAsync(name, scenarios, options, token);

    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        Func<ReportOptions, CancellationToken, Task<AssuranceVerificationResult>> verify =
            _verify ?? throw new InvalidOperationException("No scenario to verify was given.");
        try
        {
            Result = await verify(Options, cancellationToken);
        }
        catch (Exception exception)
        {
            Failure = exception;
        }
    }

    public void Dispose()
    {
        Cancellation.Dispose();
        if (_folder is not null && Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class CapturingSink : IAssuranceEvidenceSink
    {
        public ConcurrentQueue<VerificationEvidence> Evidence { get; } = new();

        public string Name => "capture";

        public Task<EvidenceDeliveryResult> PublishAsync(VerificationEvidence evidence, EvidenceDeliveryContext context, CancellationToken cancellationToken = default)
        {
            Evidence.Enqueue(evidence);
            return Task.FromResult(EvidenceDeliveryResult.Empty);
        }
    }
}

public sealed class VerificationSteps : ScenarioSteps<VerificationUnderTest>
{
    public static ScenarioAction<VerificationUnderTest> ARenewalWhoseCheckIs(AssuranceStatus status) =>
        CreateActionStep($"A renewal scenario whose check is {status}", verification => verification.Use(Renewal(status)));

    public static ScenarioAction<VerificationUnderTest> NoScenarios(string name) =>
        CreateActionStep($"A verification named '{name}' with no scenarios", verification => verification.Use<object>(name, []));

    public static ScenarioAction<VerificationUnderTest> AReportFile { get; } =
        CreateActionStep("The report is written to a file", verification => verification.Sinks.Add(Html(verification.ReportPath)));

    public static ScenarioAction<VerificationUnderTest> TheBuiltInHtmlReport { get; } =
        CreateActionStep("The built-in HTML report is written to the reports folder", verification => verification.Sinks.Add(ReportSinks.Html()));

    public static ScenarioAction<VerificationUnderTest> AFolderIsInTheWayOfTheReport { get; } =
        CreateActionStep("A folder is in the way of the report file", verification =>
        {
            _ = Directory.CreateDirectory(verification.ReportPath);
            verification.Sinks.Add(Html(verification.ReportPath));
        });

    public static ScenarioAction<VerificationUnderTest> AReportWriterThatFails(string message) =>
        CreateActionStep($"A report whose writer fails part way with '{message}'", verification => verification.Sinks.Add(
            new ArtifactReportSink(new("Custom", new BrokenReporter(message), new LocalFileArtifactDestination(verification.Folder, ".html")))));

    public static ScenarioAction<VerificationUnderTest> ItIsVerified { get; } =
        CreateActionStep("The scenario is verified through the xUnit adapter", (verification, token) => verification.VerifyAsync(token));

    public static ScenarioExpectation<VerificationUnderTest> TheTestPasses { get; } =
        CreateAssertionStep("The test passes", verification => Assert.Null(verification.Failure));

    public static ScenarioExpectation<VerificationUnderTest> TheTestFailsSaying(string text) =>
        CreateAssertionStep($"The test fails, saying '{text}'", verification => Assert.Contains(text, verification.TestFailure.Message));

    public static ScenarioExpectation<VerificationUnderTest> TheReportSays(string text) =>
        CreateAssertionStep($"The report says '{text}'", verification => Assert.Contains(text, verification.Report));

    public static ScenarioExpectation<VerificationUnderTest> TheReportDoesNotSay(string text) =>
        CreateAssertionStep($"The report doesn't say '{text}'", verification => Assert.DoesNotContain(text, verification.Report));

    public static ScenarioExpectation<VerificationUnderTest> NoReportIsLeftBehind { get; } =
        CreateAssertionStep("No report file is left behind", verification => Assert.Empty(Directory.GetFiles(verification.Folder)));

    /// <summary>A renewal whose one check finds the given verdict, in the business context of keeping a customer's access.</summary>
    public static Scenario<object> Renewal(AssuranceStatus status) =>
        Scenario.Define<object>("Renew", BusinessContext.Define("Retain customer access")
                .Capability("renewal", "Renewal")
                .Outcome("access", "Customer retains access")
                .Build(), id: "renew")
            .Given("Ready", _ => { })
            .When("Renewal", _ => { })
            .Then("Retains access", _ => new ExpectationResult(status, "Access observation", "Access", status.ToString()));

    private static ArtifactReportSink Html(string path) =>
        new(new("HTML", new StakeholderReportWriter(), LocalFileArtifactDestination.ForFile(path)));

    private sealed class BrokenReporter(string message) : IAssuranceReporter
    {
        public async Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync("partial"u8.ToArray(), cancellationToken);
            throw new IOException(message);
        }
    }
}

/// <summary>What the xUnit adapter promises about the reports a verification writes.</summary>
public static class XunitBehaviour
{
    public static BusinessContext Reports { get; } = BusinessContext.Define("xunit-reports", "Reports a reader can open",
            "Every verification leaves a report a reader can open, whatever its verdict, even when the scenario couldn't run.")
        .Capability("reports", "Verification reports")
        .Outcome("always-reported", "Every verification, passed, failed or broken, has a report")
        .Build();
}
