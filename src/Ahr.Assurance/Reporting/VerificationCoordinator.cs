using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class VerificationCoordinator
{
    public static Task<VerificationResult> VerifyAsync(
        string name, IEnumerable<Scenario> scenarios, ReportOptions options,
        VerificationContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        return VerifyAsync(name, scenarios.Select(scenario => scenario.Definition), options, context, cancellationToken);
    }

    public static async Task<VerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions options,
        VerificationContext? context = null, CancellationToken cancellationToken = default) where TContext : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(options);
        context ??= new();
        if (options.Sinks.IsDefault)
        {
            throw new ArgumentException("Sinks must be initialized.", nameof(options));
        }
        EvidencePublisher.ValidateSinks(options.Sinks);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DefaultOutputDirectory);
        var producer = options.Producer ?? "verification";
        ArgumentException.ThrowIfNullOrWhiteSpace(producer);
        if (!Enum.IsDefined(options.TextOutput))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown text output verbosity.");
        }
        if (options.RecoveryTimeout <= TimeSpan.Zero || options.RecoveryTimeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Recovery timeout must be a finite positive timer duration.");
        }
        if (options.Attachments.IsDefault)
        {
            throw new ArgumentException("Attachments must be initialized.", nameof(options));
        }
        (var correlation, var correlationSource) = RunReport.Correlation(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlation);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AttachmentsDirectory);
        if (options.UsesRunFolders)
        {
            RunFolders.Prune(Path.GetFullPath(options.DefaultOutputDirectory, AppContext.BaseDirectory), RunFolders.Name(correlation), AssuranceConfig.Current.Match(config => config.Keep, () => null));
        }

        var directory = RunReport.Folder(options, correlation);
        var attachmentsDirectory = options.Sinks.IsEmpty ? null : Path.GetFullPath(options.AttachmentsDirectory, directory);
        var id = Guid.NewGuid().ToString("N");
        AttachmentCapture? capture = attachmentsDirectory is null ? null : new(attachmentsDirectory, id);
        AssuranceRunResult? partial = null;
        AssuranceReport report;
        VerificationExecutionState state;
        OperationCanceledException? cancellation = null;
        Exception? cause = null;
        try
        {
            Result<AssuranceRunResult, AssuranceExecutionError> outcome = await new AssuranceRunner(capture).RunAsync(name, scenarios, value => partial = value, cancellationToken).ConfigureAwait(false);
            cause = outcome.Match(static _ => null, static error => error.Exception);
            (report, state) = Recorded(outcome, name);
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
            state = VerificationExecutionState.Canceled;
            report = new(partial ?? new(name, DateTimeOffset.UtcNow, TimeSpan.Zero, []),
                [new("verification-canceled", exception.Message, exception.GetType().FullName)], []);
        }
        List<ReportDiagnostic> failures = [.. report.Diagnostics];
        ImmutableArray<EvidenceAttachment> captured = capture is null ? [] : await capture.CompleteAsync().ConfigureAwait(false);
        ImmutableArray<EvidenceAttachment> attachments = options.Attachments;
        if (options.FinalizeAttachmentsAsync is { } finalize)
        {
            Result<ImmutableArray<EvidenceAttachment>, ReportDiagnostic> finalized = await FinalizeAsync(finalize, options.RecoveryTimeout).ConfigureAwait(false);
            attachments = attachments.AddRange(finalized.Match(static added => added, static _ => []));
            _ = finalized.TapError(failures.Add);
        }
        if (capture is not null && !attachments.IsEmpty)
        {
            try
            {
                using CancellationTokenSource recovery = new(options.RecoveryTimeout);
                (attachments, ImmutableArray<ReportDiagnostic> copied) = await AttachmentBundle.MaterializeAsync(
                    attachments, options.AttachmentSource, capture.Directory, id, cancellation is null ? cancellationToken : recovery.Token).ConfigureAwait(false);
                failures.AddRange(copied);
            }
            catch (OperationCanceledException exception)
            {
                cancellation ??= exception;
                failures.Add(new("report-attachment-delivery-error", exception.Message, exception.GetType().FullName));
                attachments = [.. attachments.Select(attachment => attachment with { Available = false })];
            }
        }
        attachments = attachments.AddRange(captured);
        report = report with
        {
            Diagnostics = [.. failures]
        };
        VerificationEvidence evidence = new(id, correlation, producer, context.TestId, context.TestName, state, report, attachments)
        {
            CorrelationSource = correlationSource,
            AssuranceVersion = AssuranceVersion.Current
        };
        Result valid = evidence.Check();
        if (valid.IsFailure)
        {
            failures.Add(new("report-attachment-validation-error", valid.Error.Message, typeof(InvalidDataException).FullName));
            evidence = evidence with
            {
                Attachments = [],
                Report = report with
                {
                    Diagnostics = [.. failures]
                }
            };
        }
        EvidenceDeliveryContext deliveryContext = new(directory)
        {
            AttachmentSource = attachmentsDirectory is null ? null : new AttachmentFolderSource(attachmentsDirectory),
            AttachmentsDirectory = attachmentsDirectory,
            IncludeDiagnosticDetails = options.IncludeDiagnosticDetails,
            IsRecovery = cancellation is not null
        };
        EvidenceDeliveryResult delivery;
        try
        {
            using CancellationTokenSource recovery = new(options.RecoveryTimeout);
            delivery = await EvidencePublisher.PublishAsync(evidence, options.Sinks, deliveryContext,
                cancellation is null ? cancellationToken : recovery.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            cancellation ??= exception;
            evidence = evidence with
            {
                Report = evidence.Report with
                {
                    Diagnostics = evidence.Report.Diagnostics.Add(new("report-delivery-canceled", "Evidence delivery canceled.", exception.GetType().FullName))
                }
            };
            try
            {
                using CancellationTokenSource recovery = new(options.RecoveryTimeout);
                delivery = await EvidencePublisher.PublishAsync(evidence, options.Sinks,
                    deliveryContext with
                    {
                        IsRecovery = true
                    }, recovery.Token).ConfigureAwait(false);
            }
            catch (Exception recoveryFailure)
            {
                cancellation.Data["AssurancePersistenceFailure"] = recoveryFailure;
                delivery = new([], [new("report-recovery-error", recoveryFailure.Message, recoveryFailure.GetType().FullName)]);
            }
        }
        failures.AddRange(delivery.Diagnostics);
        report = evidence.Report with
        {
            Artifacts = delivery.Artifacts,
            Diagnostics = evidence.Report.Diagnostics.AddRange(delivery.Diagnostics)
        };
        if (report.Run.Status != AssuranceStatus.Satisfied)
        {
            failures.Add(new("unsatisfied-verdict", $"Assurance verdict: {report.Run.Status}."));
            foreach (ScenarioResult scenario in report.Run.Scenarios)
            {
                foreach (StepResult? step in scenario.Steps.Where(step => step.Evaluation.Status != AssuranceStatus.Satisfied))
                {
                    failures.Add(new("step-verdict",
                        $"{scenario.Name} / {step.Name}: {step.Evaluation.Status}. {step.Evaluation.Explanation}"
                        + (step.Evaluation.Expected is { } expected ? $"\nExpected: {expected}" : "")
                        + (step.Evaluation.Actual is { } actual ? $"\nActual: {actual}" : "")));
                }
            }
        }
        if (options.TextOutput != AssuranceTextVerbosity.Off)
        {
            try
            {
                var text = new PlainTextReportWriter(options.TextOutput).RenderEvidence(EvidenceExport.Project(evidence with
                {
                    Report = report
                }, options.IncludeDiagnosticDetails));
                Result written = await WriteTextAsync(context, text, options.RecoveryTimeout).ConfigureAwait(false);
                _ = written.TapError(error => failures.Add(new("report-text-error", error.Message, error.Exception?.GetType().FullName ?? typeof(InvalidOperationException).FullName)));
            }
            catch (OperationCanceledException exception)
            {
                cancellation ??= exception;
                failures.Add(new("report-text-canceled", exception.Message, exception.GetType().FullName));
            }
            report = report with
            {
                Diagnostics = [.. report.Diagnostics, .. failures.Where(item =>
                    item.Code is "report-text-error" or "report-text-canceled")]
            };
        }
        if (cancellation is not null)
        {
            if (failures.Count != 0)
            {
                cancellation.Data["AssuranceFailures"] = string.Join(Environment.NewLine, failures.Select(item => item.Message));
            }
            ExceptionDispatchInfo.Capture(cancellation).Throw();
        }
        return new(report, [.. failures], cause);
    }

    private static async Task<Result<ImmutableArray<EvidenceAttachment>, ReportDiagnostic>> FinalizeAsync(
        Func<CancellationToken, Task<ImmutableArray<EvidenceAttachment>>> finalize, TimeSpan timeout)
    {
        using CancellationTokenSource cleanup = new(timeout);
        try
        {
            Result<ImmutableArray<EvidenceAttachment>, ReportDiagnostic> finalized = await Result.TryAsync(() => finalize(cleanup.Token), static _ => true,
                static exception => FinalizationFailure(exception.Message, exception.GetType())).ConfigureAwait(false);
            return finalized.Ensure(static attachments => !attachments.IsDefault,
                FinalizationFailure("Attachment finalization returned an uninitialized collection.", typeof(InvalidDataException)));
        }
        catch (OperationCanceledException exception)
        {
            return Result<ImmutableArray<EvidenceAttachment>, ReportDiagnostic>.Failure(FinalizationFailure(exception.Message, exception.GetType()));
        }
    }

    private static ReportDiagnostic FinalizationFailure(string message, Type type) => new("report-attachment-finalization-error", message, type.FullName);

    private static async Task<Result> WriteTextAsync(VerificationContext context, string text, TimeSpan timeout)
    {
        using CancellationTokenSource recovery = new(timeout);
        return await context.PublishTextAsync.ToOption()
            .ToResult(new Error("No current-test text sink was supplied."))
            .BindAsync(publish => Result.TryAsync(() => publish(text, recovery.Token), static _ => true)).ConfigureAwait(false);
    }

    private static (AssuranceReport Report, VerificationExecutionState State) Recorded(Result<AssuranceRunResult, AssuranceExecutionError> outcome, string name)
    {
        if (outcome.IsSuccess)
        {
            return (new AssuranceReport(outcome.Value, [], []), VerificationExecutionState.Completed);
        }

        AssuranceExecutionError error = outcome.Error;
        return (new AssuranceReport(error.PartialRun ?? new(name, DateTimeOffset.UtcNow, TimeSpan.Zero, []),
            [new(error.Code, error.Message, error.ExceptionType)], []), VerificationExecutionState.Failed);
    }
}
