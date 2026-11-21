using System.Runtime.CompilerServices;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

internal static class StepValues
{
    private static readonly ConditionalWeakTable<object, List<Given>> _given = [];

    internal static void Keep(object context, Type type, object? value)
    {
        List<Given> kept = _given.GetValue(context, static _ => []);
        lock (kept)
        {
            kept.Add(new(type, value));
        }
    }

    internal static TValue Read<TValue>(object context, string step, Option<int> paired)
    {
        if (_given.TryGetValue(context, out List<Given>? kept))
        {
            lock (kept)
            {
                if (paired.IsSome && paired.Value < kept.Count && kept[paired.Value].Is<TValue>())
                {
                    return (TValue)kept[paired.Value].Value!;
                }

                for (var index = kept.Count - 1; index >= 0; index--)
                {
                    if (kept[index].Is<TValue>())
                    {
                        return (TValue)kept[index].Value!;
                    }
                }
            }
        }

        throw new InvalidOperationException(NoEarlierStep(step, typeof(TValue)));
    }

    internal static ScenarioAction<TContext> Returning<TContext, TResult>(string name, Func<TContext, CancellationToken, Task<TResult>> work, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(work);
        if (typeof(TResult) == typeof(ValueTask))
        {
            return AwaitingValueTask(name, work, description);
        }

        if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            return (ScenarioAction<TContext>)_awaitingValueTaskOf.MakeGenericMethod(typeof(TContext), typeof(TResult).GetGenericArguments()[0])
                .Invoke(null, [name, work, description])!;
        }

        if (typeof(TContext).IsValueType)
        {
            return new(name, (context, cancellationToken) => work(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name), description);
        }

        ScenarioAction<TContext> discarding = new(name, async (context, cancellationToken) =>
        {
            try
            {
                _ = await (work(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name)).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }
        }, description);

        return new(name, async (context, cancellationToken) =>
        {
            TResult result;
            try
            {
                result = await (work(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name)).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }

            Keep(context, typeof(TResult), result);
        }, description)
        {
            Gives = typeof(TResult),
            Discarding = discarding
        };
    }

    private static ScenarioAction<TContext> AwaitingValueTask<TContext, TResult>(string name, Func<TContext, CancellationToken, Task<TResult>> work, string? description)
        where TContext : notnull =>
        new(name, async (context, cancellationToken) =>
        {
            try
            {
                var pending = (ValueTask)(object)(await (work(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name)).ConfigureAwait(false))!;
                await pending.ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }
        }, description);

    private static readonly System.Reflection.MethodInfo _awaitingValueTaskOf =
        typeof(StepValues).GetMethod(nameof(AwaitingValueTaskOf), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static ScenarioAction<TContext> AwaitingValueTaskOf<TContext, TValue>(string name, Delegate work, string? description)
        where TContext : notnull
    {
        var pending = (Func<TContext, CancellationToken, Task<ValueTask<TValue>>>)work;
        return Returning<TContext, TValue>(name, async (context, cancellationToken) =>
            await (await (pending(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name)).ConfigureAwait(false)).ConfigureAwait(false),
            description);
    }

    internal static ScenarioExpectation<TContext> Check<TContext, TValue>(string name, Func<TContext, TValue, CancellationToken, Task> check, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(check);
        RequireClass<TContext>();
        return ScenarioExpectation<TContext>.Reading<TValue>(name, paired => ScenarioExpectation<TContext>.Assertion(name,
            (context, cancellationToken) => check(context, Read<TValue>(context, name, paired), cancellationToken), description));
    }

    internal static ScenarioExpectation<TContext> Check<TContext, TValue>(string name, Func<TContext, TValue, Task> check, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(check);
        return Check<TContext, TValue>(name, (context, value, _) => check(context, value), description);
    }

    internal static ScenarioExpectation<TContext> Check<TContext, TValue>(string name, Action<TContext, TValue, CancellationToken> check, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(check);
        return Check<TContext, TValue>(name, (context, value, cancellationToken) =>
        {
            check(context, value, cancellationToken);
            return Task.CompletedTask;
        }, description);
    }

    internal static ScenarioExpectation<TContext> Check<TContext, TValue>(string name, Action<TContext, TValue> check, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(check);
        return Check<TContext, TValue>(name, (context, value, _) => check(context, value), description);
    }

    internal static string Giving(Type type)
    {
        if (typeof(Exception).IsAssignableFrom(type))
        {
            return "throws or returns";
        }

        return "returns";
    }

    internal static string Give(Type type)
    {
        if (typeof(Exception).IsAssignableFrom(type))
        {
            return "throw or return";
        }

        return "return";
    }

    internal static string NoStepInItsBlock(string step, Type type, string earlier) =>
        $"'{step}' takes the {TypeNames.Of(type)} that a step in its When and Then block {Giving(type)}, but none there {Giving(type)} one. "
        + $"'{earlier}' {Giving(type)} one in an earlier block; check it in that block, or {Give(type)} it again in this one.";

    internal static string NullableInItsBlock(string step, Type type, string giver) =>
        $"'{step}' takes the {TypeNames.Of(type)} that a step in its When and Then block returns, but none there returns one. "
        + $"'{giver}' returns {TypeNames.Of(type)}?, a different type; take {TypeNames.Of(type)}? in the check, or return the value itself from the step.";

    internal static string NoEarlierStep(string step, Type type) =>
        $"'{step}' takes the {TypeNames.Of(type)} that an earlier step {Giving(type)}, but no earlier step {Giving(type)} one.";

    private static void RequireClass<TContext>()
    {
        if (typeof(TContext).IsValueType)
        {
            throw new InvalidOperationException(
                $"A check of what an earlier step gave reads it from the scenario's context, so the context must be a class; {TypeNames.Of(typeof(TContext))} is a struct.");
        }
    }

    private readonly record struct Given(Type Type, object? Value)
    {
        internal bool Is<TValue>() => typeof(TValue).IsAssignableFrom(Type);
    }
}
