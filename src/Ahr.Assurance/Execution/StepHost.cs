using System.Collections.Immutable;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Execution;

internal sealed class StepHost(Func<Func<Task>, Task>? runStep = null, ImmutableArray<StepParameter> example = default)
{
    private static readonly AsyncLocal<StepHost?> _current = new();

    internal static StepHost? Current => _current.Value;

    internal Exception? Skip { get; set; }

    internal Exception? Failure { get; set; }

    internal ImmutableArray<StepParameter> Example { get; } = example.IsDefault ? [] : example;

    internal Task RunAsync(Func<Task> step) => runStep is null ? step() : runStep(step);

    internal static Scope Enter(StepHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        StepHost? previous = _current.Value;
        _current.Value = host;
        return new(previous);
    }

    internal readonly struct Scope(StepHost? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
