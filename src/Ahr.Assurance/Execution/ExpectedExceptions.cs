namespace Ahr.Assurance.Execution;

internal static class ExpectedExceptions
{
    internal static ScenarioAction<TContext> Step<TContext, TException>(string name, Func<TContext, CancellationToken, Task> work, string? description)
        where TContext : notnull
        where TException : Exception =>
        Step(name, typeof(TException), work, description);

    internal static ScenarioAction<TContext> Step<TContext>(string name, Type exception, Func<TContext, CancellationToken, Task> work, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!typeof(Exception).IsAssignableFrom(exception))
        {
            throw new ArgumentException($"A throws step expects an exception type; {TypeNames.Of(exception)} is not one.", nameof(exception));
        }

        return Catching(name, new Expected(exception, OrDerived: false), work, description);
    }

    internal static ScenarioAction<TContext> StepAny<TContext, TException>(string name, Func<TContext, CancellationToken, Task> work, string? description)
        where TContext : notnull
        where TException : Exception =>
        Catching(name, new Expected(typeof(TException), OrDerived: true), work, description);

    internal static ScenarioAction<TContext> ReturningAny<TContext, TException>(string name, Func<TContext, object?> work, string? description)
        where TContext : notnull
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(work);
        return StepAny<TContext, TException>(name, (context, _) => Task.FromResult(work(context)), description);
    }

    private static ScenarioAction<TContext> Catching<TContext>(string name, Expected expected, Func<TContext, CancellationToken, Task> work, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(work);
        RequireClass<TContext>();
        return new(name, async (context, cancellationToken) =>
        {
            Exception thrown = await CatchAsync(expected, () => work(context, cancellationToken), cancellationToken).ConfigureAwait(false);
            StepValues.Keep(context, expected.Type, thrown);
        }, description)
        {
            Gives = expected.Type,
            Checks = true
        };
    }

    internal static ScenarioAction<TContext> Returning<TContext, TException>(string name, Func<TContext, object?> work, string? description)
        where TContext : notnull
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(work);
        return Step<TContext, TException>(name, (context, _) => Task.FromResult(work(context)), description);
    }

    internal static ScenarioAction<TContext> Returning<TContext>(string name, Type exception, Func<TContext, object?> work, string? description)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(work);
        return Step<TContext>(name, exception, (context, _) => Task.FromResult(work(context)), description);
    }

    private static async Task<Exception> CatchAsync(Expected expected, Func<Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception exception) when (expected.Matches(exception))
        {
            return exception;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ExpectedExceptionNotThrownException(
                $"Expected {expected}, but {TypeNames.Of(exception.GetType())} was thrown: {exception.Message}", exception);
        }
        finally
        {
            AttachmentScope.KeepStepContext();
        }

        throw new ExpectedExceptionNotThrownException($"Expected {expected}, but nothing was thrown.");
    }

    private readonly record struct Expected(Type Type, bool OrDerived)
    {
        internal bool Matches(Exception exception) => OrDerived ? Type.IsInstanceOfType(exception) : exception.GetType() == Type;

        public override string ToString() => OrDerived ? $"{TypeNames.Of(Type)} or a type derived from it" : TypeNames.Of(Type);
    }

    private static void RequireClass<TContext>()
    {
        if (typeof(TContext).IsValueType)
        {
            throw new InvalidOperationException(
                $"Throws steps keep the exception with the scenario's context, so the context must be a class; {TypeNames.Of(typeof(TContext))} is a struct.");
        }
    }
}

internal sealed class ExpectedExceptionNotThrownException : Exception
{
    public ExpectedExceptionNotThrownException(string message)
        : base(message)
    {
    }

    public ExpectedExceptionNotThrownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
