using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

internal readonly record struct StepOutcome(string Scenario, string Step, StepKind Kind, AssuranceStatus Status, bool StoppedExecution)
{
    internal bool Failed => StoppedExecution || Status == AssuranceStatus.Unsatisfied;
}

internal readonly record struct ScenarioOutcome(string Scenario, AssuranceStatus Status, bool StoppedExecution)
{
    internal bool Failed => StoppedExecution || Status == AssuranceStatus.Unsatisfied;
}

internal interface IStepObserver
{
    Task ScenarioStartingAsync(string scenario, CancellationToken cancellationToken);

    Task StepFinishedAsync(StepOutcome step, CancellationToken cancellationToken);

    Task ScenarioFinishedAsync(ScenarioOutcome scenario, CancellationToken cancellationToken);
}

internal static class StepObservers
{
    private static readonly AsyncLocal<ImmutableArray<IStepObserver>> _current = new();

    internal static ImmutableArray<IStepObserver> Current => _current.Value.IsDefault ? [] : _current.Value;

    internal static IDisposable Add(IStepObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ImmutableArray<IStepObserver> previous = Current;
        _current.Value = previous.Add(observer);
        return new Scope(previous);
    }

    internal static async Task ScenarioStartingAsync(ImmutableArray<IStepObserver> observers, string scenario, CancellationToken cancellationToken)
    {
        foreach (IStepObserver observer in observers)
        {
            await ObserveAsync(observer, () => observer.ScenarioStartingAsync(scenario, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task StepFinishedAsync(ImmutableArray<IStepObserver> observers, StepOutcome step, CancellationToken cancellationToken)
    {
        foreach (IStepObserver observer in observers)
        {
            await ObserveAsync(observer, () => observer.StepFinishedAsync(step, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task ScenarioFinishedAsync(ImmutableArray<IStepObserver> observers, ScenarioOutcome scenario, CancellationToken cancellationToken)
    {
        foreach (IStepObserver observer in observers)
        {
            await ObserveAsync(observer, () => observer.ScenarioFinishedAsync(scenario, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ObserveAsync(IStepObserver observer, Func<Task> call, CancellationToken cancellationToken)
    {
        Result observed;
        try
        {
            observed = await Result.TryAsync(call, _ => !cancellationToken.IsCancellationRequested).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            observed = exception.ToFailure();
        }

        _ = observed.TapError(error => Attach.Text($"{observer.GetType().Name} failed.txt",
            $"{error.Exception?.GetType().FullName}: {error.Message}"));
    }

    private sealed class Scope(ImmutableArray<IStepObserver> previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
