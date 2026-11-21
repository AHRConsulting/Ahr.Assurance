using Ahr.Assurance.Core;
using Ahr.Foundation;
namespace Ahr.Assurance.Execution;

internal static class ScenarioExecutionExtensions
{
    internal static Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync<TContext>(
        this Scenario<TContext> scenario,
        CancellationToken cancellationToken = default)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new AssuranceRunner().RunAsync(scenario.Name, [scenario], cancellationToken);
    }

    internal static Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync<TContext>(
        this IEnumerable<Scenario<TContext>> scenarios,
        string name,
        CancellationToken cancellationToken = default)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        return new AssuranceRunner().RunAsync(name, scenarios, cancellationToken);
    }
}
