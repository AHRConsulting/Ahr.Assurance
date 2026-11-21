using System.Collections.Concurrent;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Ahr.Assurance.TestingPlatform;

internal static class AssurancePlatform
{
    internal static void AddExtensions(ITestApplicationBuilder builder, Func<ReportOptions> options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.CommandLine.AddProvider(() => new AssuranceCommandLine());
        CompositeExtensionFactory<RunRecorder> recorder = new(services => new RunRecorder(services, options));
        builder.TestHost.AddDataConsumer(recorder);
        builder.TestHost.AddTestSessionLifetimeHandle(recorder);
    }
}

internal sealed class AssuranceCommandLine : ICommandLineOptionsProvider
{
    internal const string Report = "assurance-report";
    internal const string Title = "assurance-title";

    public string Uid => "Ahr.Assurance.CommandLine";
    public string Version => AssuranceVersion.Current;
    public string DisplayName => "Ahr.Assurance";
    public string Description => "Options for the Ahr.Assurance report written when the test run ends.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(Report, "Write the Ahr.Assurance report when the test run ends: 'on' (the default) or 'off'.", new ArgumentArity(1, 1), false),
        new(Title, "The title of the Ahr.Assurance report written when the test run ends.", new ArgumentArity(1, 1), false)
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) =>
        commandOption.Name == Report && arguments is not ["on"] and not ["off"]
            ? ValidationResult.InvalidTask($"--{Report} takes 'on' or 'off'.")
            : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => ValidationResult.ValidTask;
}

internal sealed class RunRecorder(IServiceProvider services, Func<ReportOptions> options)
    : IDataConsumer, IDataProducer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private readonly ConcurrentDictionary<string, (string Name, Outcome Outcome)> _tests = new(StringComparer.Ordinal);

    internal enum Outcome
    {
        Passed,
        Failed,
        Skipped,
    }

    public string Uid => "Ahr.Assurance.RunRecorder";
    public string Version => AssuranceVersion.Current;
    public string DisplayName => "Ahr.Assurance";
    public string Description => "Records every test's outcome and writes the Ahr.Assurance report when the test run ends.";
    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];
    public Type[] DataTypesProduced => [typeof(SessionFileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is TestNodeUpdateMessage { TestNode: { } node } && Classify(node.Properties.SingleOrDefault<TestNodeStateProperty>()) is { } outcome)
        {
            _tests[node.Uid.Value] = (node.DisplayName, outcome);
        }

        return Task.CompletedTask;
    }

#if TESTING_PLATFORM_1
    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => FinishAsync(sessionUid, cancellationToken);
#else
    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => Task.CompletedTask;

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        ArgumentNullException.ThrowIfNull(testSessionContext);
        return FinishAsync(testSessionContext.SessionUid, testSessionContext.CancellationToken);
    }
#endif

    private async Task FinishAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        ICommandLineOptions commandLine = services.GetCommandLineOptions();
        var write = AssuranceConfig.Current.Map(config => config.ReportAfterRun).GetValueOrDefault();
        if (commandLine.TryGetOptionArgumentList(AssuranceCommandLine.Report, out var report) && report is [var choice])
        {
            write = choice == "on";
        }

        var title = commandLine.TryGetOptionArgumentList(AssuranceCommandLine.Title, out var titles) && titles is [var named] ? named : null;
        Result written = await Result.TryAsync(() => WriteReportAsync(sessionUid, title, write, cancellationToken), static _ => true).ConfigureAwait(false);
        _ = await written.MatchAsync(static () => Task.CompletedTask, error => WarnAsync(error.Message, cancellationToken)).ConfigureAwait(false);
    }

    private async Task WriteReportAsync(SessionUid sessionUid, string? title, bool write, CancellationToken cancellationToken)
    {
        Option<string> written = await RunReport.WriteAsync(options(), Summary(), title, write, cancellationToken).ConfigureAwait(false);
        _ = await written.TapAsync(path => services.GetMessageBus().PublishAsync(this, new SessionFileArtifact(sessionUid, new FileInfo(path),
            "Assurance report", "The Ahr.Assurance report for this test run."))).ConfigureAwait(false);
    }

    private async Task WarnAsync(string problem, CancellationToken cancellationToken)
    {
        TextOutputDeviceData warning = new($"Warning: the Assurance report could not be written: {problem}");
#if TESTING_PLATFORM_1
        await services.GetOutputDevice().DisplayAsync(this, warning).ConfigureAwait(false);
#else
        await services.GetOutputDevice().DisplayAsync(this, warning, cancellationToken).ConfigureAwait(false);
#endif
    }

    internal TestRunSummary Summary()
    {
        (string Name, Outcome Outcome)[] tests = [.. _tests.Values];
        return new("", tests.Count(test => test.Outcome == Outcome.Passed), tests.Count(test => test.Outcome == Outcome.Failed),
            tests.Count(test => test.Outcome == Outcome.Skipped),
            [.. tests.Where(test => test.Outcome == Outcome.Failed).Select(test => test.Name).Order(StringComparer.Ordinal)]);
    }

    internal static Outcome? Classify(TestNodeStateProperty? state) => state switch
    {
        PassedTestNodeStateProperty => Outcome.Passed,
        SkippedTestNodeStateProperty => Outcome.Skipped,
        FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty => Outcome.Failed,
        _ => null
    };
}
