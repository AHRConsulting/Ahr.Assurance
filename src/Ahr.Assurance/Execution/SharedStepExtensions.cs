namespace Ahr.Assurance.Execution;

/// <summary>
/// Steps written for an interface or base type, used in a scenario whose context implements or derives from it.
/// A context that doesn't fails to compile. The context must be a class, so a step's changes reach the scenario's own context.
/// </summary>
public static class SharedStepExtensions
{
    /// <summary>Appends setup work written for <typeparamref name="TShared"/>.</summary>
    public static Scenario<TContext> Given<TContext, TShared>(this Scenario<TContext> scenario, ScenarioAction<TShared> action)
        where TContext : class, TShared
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(action);
        return scenario.Given(action.For<TContext>());
    }

    /// <summary>Appends operation work written for <typeparamref name="TShared"/>.</summary>
    public static Scenario<TContext> When<TContext, TShared>(this Scenario<TContext> scenario, ScenarioAction<TShared> action)
        where TContext : class, TShared
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(action);
        return scenario.When(action.For<TContext>());
    }

    /// <summary>Appends a check written for <typeparamref name="TShared"/>.</summary>
    public static Scenario<TContext> Then<TContext, TShared>(this Scenario<TContext> scenario, ScenarioExpectation<TShared> expectation)
        where TContext : class, TShared
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(expectation);
        return scenario.Then(expectation.For<TContext>());
    }

    /// <summary>After Given or When, adds more of that work, written for <typeparamref name="TShared"/>.</summary>
    public static Scenario<TContext> And<TContext, TShared>(this Scenario<TContext> scenario, ScenarioAction<TShared> action)
        where TContext : class, TShared
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(action);
        return scenario.And(action.For<TContext>());
    }

    /// <summary>After Then, adds another check written for <typeparamref name="TShared"/>.</summary>
    public static Scenario<TContext> And<TContext, TShared>(this Scenario<TContext> scenario, ScenarioExpectation<TShared> expectation)
        where TContext : class, TShared
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(expectation);
        return scenario.And(expectation.For<TContext>());
    }
}
