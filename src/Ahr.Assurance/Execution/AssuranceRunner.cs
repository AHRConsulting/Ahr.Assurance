using System.Collections.Immutable;
using System.Diagnostics;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

internal sealed class AssuranceRunner(AttachmentCapture? attachments = null)
{
    internal const string StoppedHereLabel = "This step stopped execution";
    internal const string StoppedHere = StoppedHereLabel + ": ";
    internal const string NotRun = "Not run; execution stopped: ";
    internal const string NotRunAfterFailure = "Not run; an earlier step failed.";
    internal const string NotRunAfterSkip = "Not run; the test was skipped.";
    internal const string NotRunAfterFailedCheck = "Not run; a check before this step failed.";

    private enum StepStop
    {
        None,
        Failed,
        FailedCheck,
        Skipped,
    }

    public async Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios,
        CancellationToken cancellationToken = default)
        where TContext : notnull
        => await RunAsync(name, scenarios, null, cancellationToken).ConfigureAwait(false);

    internal async Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync<TContext>(
        string name,
        IEnumerable<Scenario<TContext>> scenarios,
        Action<AssuranceRunResult>? onCancellation,
        CancellationToken cancellationToken)
        where TContext : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scenarios);
        cancellationToken.ThrowIfCancellationRequested();
        Scenario<TContext>[] definitions = [.. scenarios];
        if (definitions.Length == 0)
        {
            return Result<AssuranceRunResult, AssuranceExecutionError>.Failure(new("invalid-definition", "An assurance run requires at least one scenario."));
        }

        HashSet<string>? ids = definitions.Length > 1 ? new(StringComparer.Ordinal) : null;
        foreach (Scenario<TContext> scenario in definitions)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            Result valid = scenario.Validate();
            Result validation = valid.IsSuccess ? Checks.That(ids?.Add(scenario.Id) ?? true, scenario, static item => $"Duplicate scenario id '{item.Id}'.") : valid;
            if (validation.IsFailure)
            {
                return Result<AssuranceRunResult, AssuranceExecutionError>.Failure(new("invalid-definition", validation.Error.Message));
            }
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        List<ScenarioResult> completed = [];
        List<StepResult> steps = [];
        var currentIndex = 0;
        var stepRunning = false;

        try
        {
            return await Result.TryAsync(ExecuteAsync, static _ => true, CreateError).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (onCancellation is not null)
        {
            onCancellation(CreateError(exception).PartialRun!);
            throw;
        }

        async Task<AssuranceRunResult> ExecuteAsync()
        {
            using var scope = AttachmentScope.Enter(attachments);
            var runContext = ExecutionContext.Capture();

            var onCallersContext = SynchronizationContext.Current is not null;
            var skipped = false;
            ImmutableArray<IStepObserver> observers = StepObservers.Current;
            for (; currentIndex < definitions.Length; currentIndex++)
            {
                if (skipped)
                {
                    completed.Add(NotRunScenario(definitions[currentIndex], [], NotRunAfterSkip));
                    continue;
                }

                Scenario<TContext> scenario = definitions[currentIndex];
                StartFrom(runContext);
                steps.Clear();
                scope.EnterScenario(ExampleRow.Id(scenario.Id));
                cancellationToken.ThrowIfCancellationRequested();
                TContext context = await scenario.CreateContextAsync(cancellationToken).ConfigureAwait(onCallersContext);
                ArgumentNullException.ThrowIfNull(context);
                StepStop stop = StepStop.None;
                var finished = false;
                try
                {
                    if (!observers.IsEmpty)
                    {
                        await StepObservers.ScenarioStartingAsync(observers, scenario.Name, cancellationToken).ConfigureAwait(onCallersContext);
                    }

                    foreach (ScenarioStep<TContext> step in scenario.Steps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (FollowsAFailedCheck(step, steps))
                        {
                            stop = StepStop.FailedCheck;
                            break;
                        }

                        var stepTimer = Stopwatch.StartNew();
                        scope.EnterStep(steps.Count);
                        stepRunning = true;
                        ExpectationResult evaluation;
                        try
                        {
                            (evaluation, stop) = await ExecuteStepAsync(step, context, cancellationToken).ConfigureAwait(onCallersContext);
                            scope.RestoreStepContext();
                        }
                        catch (Exception) when (!observers.IsEmpty && !cancellationToken.IsCancellationRequested)
                        {
                            await StepObservers.StepFinishedAsync(observers,
                                new(scenario.Name, step.Name, step.Kind, AssuranceStatus.NotEvaluated, StoppedExecution: true), cancellationToken).ConfigureAwait(onCallersContext);
                            throw;
                        }

                        stepRunning = false;
                        if (!observers.IsEmpty)
                        {
                            await StepObservers.StepFinishedAsync(observers,
                                new(scenario.Name, step.Name, step.Kind, evaluation.Status, StoppedExecution: false), cancellationToken).ConfigureAwait(onCallersContext);
                        }

                        scope.ExitStep();

                        steps.Add(new(step.Name, step.Kind, evaluation, stepTimer.Elapsed, step.Description, step.Keyword)
                        {
                            Parameters = step.Parameters
                        });
                        if (stop != StepStop.None)
                        {
                            break;
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    finished = true;
                }
                finally
                {
                    scope.ExitStep();
                    if (!observers.IsEmpty && !cancellationToken.IsCancellationRequested)
                    {
                        AssuranceStatus status = finished ? Verdict.Aggregate(steps.Select(item => item.Evaluation.Status)) : AssuranceStatus.NotEvaluated;
                        await StepObservers.ScenarioFinishedAsync(observers, new(scenario.Name, status, StoppedExecution: !finished), cancellationToken)
                            .ConfigureAwait(onCallersContext);
                    }

                    if (context is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(onCallersContext);
                    }
                    else if (context is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                completed.Add(NotRunScenario(scenario, [.. steps], NotRunReason(stop)));
                scope.FinishScenario(completed[^1]);
                skipped = stop == StepStop.Skipped;
            }

            return Snapshot();
        }

        static async ValueTask<(ExpectationResult Evaluation, StepStop Stop)> ExecuteStepAsync(
            ScenarioStep<TContext> step, TContext context, CancellationToken cancellationToken)
        {
            StepHost? host = StepHost.Current;
            try
            {
                ExpectationResult? evaluation = null;
                if (host is null)
                {
                    try
                    {
                        evaluation = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        AttachmentScope.KeepStepContext();
                    }
                }
                else
                {
                    await host.RunAsync(async () =>
                    {
                        try
                        {
                            evaluation = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            AttachmentScope.KeepStepContext();
                        }
                    }).ConfigureAwait(false);
                }

                ArgumentNullException.ThrowIfNull(evaluation);
                return (evaluation, StepStop.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException && AssertionRecognition.Classify(exception) is { } recognised)
            {
                if (recognised.Kind == RecognisedKind.Skip)
                {
                    host?.Skip ??= exception;
                    return (new(AssuranceStatus.Inconclusive, "Skipped: " + recognised.Message), StepStop.Skipped);
                }

                host?.Failure ??= exception;
                return (new(AssuranceStatus.Unsatisfied, $"{recognised.Library} assertion failed: {recognised.Message}"),
                    step.Kind == StepKind.Then ? StepStop.None : StepStop.Failed);
            }
        }

        static void StartFrom(ExecutionContext? runContext)
        {
            if (runContext is not null)
            {
                ExecutionContext.Restore(runContext);
            }
        }

        static bool FollowsAFailedCheck(ScenarioStep<TContext> step, List<StepResult> previous) =>
            step.Kind == StepKind.When && previous.Exists(result => result.Kind == StepKind.Then && result.Evaluation.Status != AssuranceStatus.Satisfied);

        static string NotRunReason(StepStop stop) => stop switch
        {
            StepStop.Skipped => NotRunAfterSkip,
            StepStop.FailedCheck => NotRunAfterFailedCheck,
            StepStop.None or StepStop.Failed => NotRunAfterFailure,
            _ => NotRunAfterFailure
        };

        static ScenarioResult NotRunScenario(Scenario<TContext> scenario, ImmutableArray<StepResult> previous, string reason) =>
            new(scenario.Id, scenario.Name, scenario.Business, previous.AddRange(scenario.Steps.Skip(previous.Length).Select(step =>
                new StepResult(step.Name, step.Kind, new(AssuranceStatus.NotEvaluated, reason), TimeSpan.Zero, step.Description, step.Keyword)
                {
                    Parameters = step.Parameters
                })),
                scenario.Description)
            {
                References = scenario.References
            };

        AssuranceExecutionError CreateError(Exception exception)
        {
            for (var index = currentIndex; index < definitions.Length; index++)
            {
                Scenario<TContext> scenario = definitions[index];
                ImmutableArray<StepResult> previous = index == currentIndex ? [.. steps] : [];
                var stoppedHere = index == currentIndex && stepRunning;
                ImmutableArray<StepResult> remaining = [.. scenario.Steps.Skip(previous.Length).Select((step, offset) =>
                    new StepResult(step.Name, step.Kind,
                        new(AssuranceStatus.NotEvaluated, (stoppedHere && offset == 0 ? StoppedHere : NotRun) + exception.Message),
                        TimeSpan.Zero, step.Description, step.Keyword)
                    {
                        IsExecutionInterrupted = true,
                        Parameters = step.Parameters
                    })];
                completed.Add(new(scenario.Id, scenario.Name, scenario.Business, previous.AddRange(remaining), scenario.Description)
                {
                    References = scenario.References
                });
            }

            return new("execution-error", exception.Message, exception.GetType().FullName, Snapshot()) { Exception = exception };
        }

        AssuranceRunResult Snapshot() => new(name, startedAt, timer.Elapsed, [.. completed.Select(ExampleRow.Apply)]);
    }
}
