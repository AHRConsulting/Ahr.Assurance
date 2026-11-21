using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

/// <summary>A single-use inline composition over Fact-local variables. The caller owns captured resources.</summary>
public sealed class Scenario
{
    internal Scenario(Scenario<object> definition) => Definition = definition;

    internal Scenario<object> Definition { get; }

    /// <summary>Defines a local scenario without a context class. A name is required; business metadata is optional.</summary>
    public static Scenario Define(
        string name,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
    {
        var executed = 0;
        return new(Scenario<object>.Define(name, () => Interlocked.Exchange(ref executed, 1) != 0
                ? throw new InvalidOperationException(
                    "Inline scenarios capture local state and can only execute once. Define a fresh scenario or use a typed context factory.")
                : new object(), business, id, description));
    }

    /// <summary>Defines a reusable scenario, inferring the context type from its fresh-state factory.</summary>
    public static Scenario<TContext> Define<TContext>(
        string name,
        Func<TContext> createContext,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
        where TContext : notnull =>
        Scenario<TContext>.Define(name, createContext, business, id, description);

    /// <summary>Defines a reusable scenario whose context is created fresh with its parameterless constructor for each run.</summary>
    public static Scenario<TContext> Define<TContext>(
        string name,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
        where TContext : notnull, new() =>
        Scenario<TContext>.Define(name, static () => new TContext(), business, id, description);

    /// <summary>Defines a reusable scenario with an asynchronous, token-aware fresh-context factory.</summary>
    public static Scenario<TContext> Define<TContext>(
        string name,
        Func<CancellationToken, Task<TContext>> createContextAsync,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
        where TContext : notnull =>
        Scenario<TContext>.Define(name, createContextAsync, business, id, description);

    /// <summary>Appends setup work. Ordinary assertions in it act as preconditions.</summary>
    public Scenario Given(string name, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.Given(name, _ => action(), description));
    }

    /// <summary>Appends asynchronous setup work. Ordinary assertions in it act as preconditions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario Given(string name, Func<Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.Given(name, _ => action(), description));
    }

    /// <summary>Appends asynchronous setup work that takes the run's cancellation token.</summary>
    public Scenario Given(string name, Func<CancellationToken, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.Given(name, (_, token) => action(token), description));
    }

    internal Scenario Given(string name, Action<CancellationToken> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.Given(name, (_, token) => action(token), description));
    }

    /// <summary>Appends operation work. Ordinary assertions in it, such as an expected exception, are checks on that step.</summary>
    public Scenario When(string name, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(name, _ => action(), description));
    }

    /// <summary>Appends asynchronous operation work.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario When(string name, Func<Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(name, _ => action(), description));
    }

    /// <summary>Appends asynchronous operation work that takes the run's cancellation token.</summary>
    public Scenario When(string name, Func<CancellationToken, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(name, (_, token) => action(token), description));
    }

    internal Scenario When(string name, Action<CancellationToken> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(name, (_, token) => action(token), description));
    }

    /// <summary>Appends operation work that returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    public Scenario When<TResult>(string name, Func<TResult> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(StepValues.Returning<object, TResult>(name, (_, _) => Task.FromResult(action()), description)));
    }

    /// <summary>Appends asynchronous operation work that returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario When<TResult>(string name, Func<Task<TResult>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(StepValues.Returning<object, TResult>(name, (_, _) => action(), description)));
    }

    /// <summary>Appends asynchronous operation work that takes the run's cancellation token and returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario When<TResult>(string name, Func<CancellationToken, Task<TResult>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(StepValues.Returning<object, TResult>(name, (_, token) => action(token), description)));
    }

    /// <summary>Appends a check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario Then(string name, Func<ExpectationResult> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.Then(name, _ => evaluate(), description));
    }

    /// <summary>Appends an asynchronous check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario Then(string name, Func<Task<ExpectationResult>> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.Then(name, _ => evaluate(), description));
    }

    /// <summary>Appends an asynchronous check that takes the run's cancellation token and returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario Then(string name, Func<CancellationToken, Task<ExpectationResult>> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.Then(name, (_, token) => evaluate(token), description));
    }

    /// <summary>After Then, adds another check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario And(string name, Func<ExpectationResult> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.And(name, _ => evaluate(), description));
    }

    /// <summary>After Then, adds another asynchronous check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario And(string name, Func<Task<ExpectationResult>> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.And(name, _ => evaluate(), description));
    }

    /// <summary>After Then, adds another asynchronous check that takes the run's cancellation token and returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario And(string name, Func<CancellationToken, Task<ExpectationResult>> evaluate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(Definition.And(name, (_, token) => evaluate(token), description));
    }

    /// <summary>Appends a check made of ordinary assertions, from any test framework or assertion library.</summary>
    public Scenario Then(string name, Action assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return new(Definition.Then(ScenarioExpectation<object>.Assertion(name, _ => assertion(), description)));
    }

    /// <summary>Appends an asynchronous check made of ordinary assertions, from any test framework or assertion library.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario Then(string name, Func<Task> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return new(Definition.Then(ScenarioExpectation<object>.Assertion(name, _ => assertion(), description)));
    }

    /// <summary>Appends an asynchronous check made of ordinary assertions that takes the run's cancellation token.</summary>
    public Scenario Then(string name, Func<CancellationToken, Task> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return new(Definition.Then(ScenarioExpectation<object>.Assertion(name, (_, token) => assertion(token), description)));
    }

    internal Scenario Then(string name, Action<CancellationToken> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return new(Definition.Then(ScenarioExpectation<object>.Assertion(name, (_, token) => assertion(token), description)));
    }

    /// <summary>After Given or When, adds more of that work; after Then, adds another check made of ordinary assertions.</summary>
    public Scenario And(string name, Action step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(ScenarioAction<object>.Synchronous(name, _ => step(), description),
            ScenarioExpectation<object>.Assertion(name, _ => step(), description)));
    }

    /// <summary>After Given or When, adds more of that work, run asynchronously; after Then, adds another asynchronous check made of ordinary assertions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario And(string name, Func<Task> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(ScenarioAction<object>.Asynchronous(name, _ => step(), description),
            ScenarioExpectation<object>.Assertion(name, _ => step(), description)));
    }

    /// <summary>After Given or When, adds more of that work; after Then, adds another check made of ordinary assertions. The step is asynchronous and takes the run's cancellation token.</summary>
    public Scenario And(string name, Func<CancellationToken, Task> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(new ScenarioAction<object>(name, (_, token) => step(token), description),
            ScenarioExpectation<object>.Assertion(name, (_, token) => step(token), description)));
    }

    internal Scenario And(string name, Action<CancellationToken> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(ScenarioAction<object>.Synchronous(name, (_, token) => step(token), description),
            ScenarioExpectation<object>.Assertion(name, (_, token) => step(token), description)));
    }

    /// <summary>After Given or When, adds more work that returns a value, which the checks after it can take; after Then, adds another check
    /// made of ordinary assertions.</summary>
    public Scenario And<TResult>(string name, Func<TResult> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(name, _ => step(), description));
    }

    /// <summary>After Given or When, adds more asynchronous work that returns a value, which the checks after it can take; after Then, adds another
    /// asynchronous check made of ordinary assertions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario And<TResult>(string name, Func<Task<TResult>> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(name, _ => step(), description));
    }

    /// <summary>After Given or When, adds more asynchronous work that returns a value, which the checks after it can take; after Then, adds another
    /// asynchronous check made of ordinary assertions. The step takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario And<TResult>(string name, Func<CancellationToken, Task<TResult>> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(Definition.And(name, (_, token) => step(token), description));
    }

    /// <summary>Appends operation work that must throw exactly <typeparamref name="TException"/>, as <c>Assert.Throws</c> checks. Anything
    /// else, or nothing, fails this check and stops the scenario. Later Then and And steps can take the exception, typed.</summary>
    public Scenario WhenThrows<TException>(string name, Action action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.Step<object, TException>(name, (_, _) =>
        {
            action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>Async work that must throw exactly <typeparamref name="TException"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario WhenThrows<TException>(string name, Func<Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.Step<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>Async work that takes the run's cancellation token and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario WhenThrows<TException>(string name, Func<CancellationToken, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.Step<object, TException>(name, (_, token) => action(token), description)));
    }

    /// <summary>Work that returns a value, such as a property read, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario WhenThrows<TException>(string name, Func<object?> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.Step<object, TException>(name, (_, _) =>
        {
            _ = action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>Async work that returns a value, which is not kept, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario WhenThrows<TException>(string name, Func<Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.Step<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>After Given or When, adds more work that must throw exactly <typeparamref name="TException"/>; see
    /// <see cref="WhenThrows{TException}(string, Action, string?)"/>.</summary>
    public Scenario AndThrows<TException>(string name, Action action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.Step<object, TException>(name, (_, _) =>
        {
            action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>After Given or When, more async work that must throw exactly <typeparamref name="TException"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario AndThrows<TException>(string name, Func<Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.Step<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>After Given or When, more async work that takes the run's cancellation token and must
    /// throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario AndThrows<TException>(string name, Func<CancellationToken, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.Step<object, TException>(name, (_, token) => action(token), description)));
    }

    /// <summary>Work that returns a value, such as a property read, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario AndThrows<TException>(string name, Func<object?> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.Step<object, TException>(name, (_, _) =>
        {
            _ = action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>After Given or When, more async work that returns a value, which is not kept, and must
    /// throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario AndThrows<TException>(string name, Func<Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.Step<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>Appends operation work that must
    /// throw <typeparamref name="TException"/> or a type derived from it, as <c>Assert.ThrowsAny</c> checks. Anything
    /// else, or nothing, fails this check and stops the scenario. Later Then and And steps can take the exception as <typeparamref name="TException"/>.</summary>
    public Scenario WhenThrowsAny<TException>(string name, Action action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.StepAny<object, TException>(name, (_, _) =>
        {
            action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>Async work that must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario WhenThrowsAny<TException>(string name, Func<Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.StepAny<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>Async work that takes the run's cancellation token and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario WhenThrowsAny<TException>(string name, Func<CancellationToken, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.StepAny<object, TException>(name, (_, token) => action(token), description)));
    }

    /// <summary>Work that returns a value, such as a property read, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario WhenThrowsAny<TException>(string name, Func<object?> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.StepAny<object, TException>(name, (_, _) =>
        {
            _ = action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>Async work that returns a value, which is not kept, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario WhenThrowsAny<TException>(string name, Func<Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.When(ExpectedExceptions.StepAny<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>After Given or When, adds more work that must throw <typeparamref name="TException"/> or a type derived from it; see
    /// <see cref="WhenThrowsAny{TException}(string, Action, string?)"/>.</summary>
    public Scenario AndThrowsAny<TException>(string name, Action action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.StepAny<object, TException>(name, (_, _) =>
        {
            action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>After Given or When, more async work that must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario AndThrowsAny<TException>(string name, Func<Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.StepAny<object, TException>(name, (_, _) => action(), description)));
    }

    /// <summary>After Given or When, more async work that takes the run's cancellation token and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario AndThrowsAny<TException>(string name, Func<CancellationToken, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.StepAny<object, TException>(name, (_, token) => action(token), description)));
    }

    /// <summary>Work that returns a value, such as a property read, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario AndThrowsAny<TException>(string name, Func<object?> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.StepAny<object, TException>(name, (_, _) =>
        {
            _ = action();
            return Task.CompletedTask;
        }, description)));
    }

    /// <summary>After Given or When, more async work that returns a value, which is not kept, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario AndThrowsAny<TException>(string name, Func<Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.And(ExpectedExceptions.StepAny<object, TException>(name, (_, _) => action(), description)));
    }

    internal Scenario WhenThrows(string name, Type exception, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.WhenThrows(name, exception, _ => action(), description));
    }

    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    internal Scenario WhenThrows(string name, Type exception, Func<Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.WhenThrows(name, exception, _ => action(), description));
    }

    internal Scenario WhenThrows(string name, Type exception, Func<CancellationToken, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.WhenThrows(name, exception, (_, token) => action(token), description));
    }

    internal Scenario WhenThrows(string name, Type exception, Func<object?> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.WhenThrows(name, exception, _ => action(), description));
    }

    internal Scenario WhenThrows(string name, Type exception, Func<Task<object?>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.WhenThrows(name, exception, _ => action(), description));
    }

    internal Scenario AndThrows(string name, Type exception, Action action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.AndThrows(name, exception, _ => action(), description));
    }

    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    internal Scenario AndThrows(string name, Type exception, Func<Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.AndThrows(name, exception, _ => action(), description));
    }

    internal Scenario AndThrows(string name, Type exception, Func<CancellationToken, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.AndThrows(name, exception, (_, token) => action(token), description));
    }

    internal Scenario AndThrows(string name, Type exception, Func<object?> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.AndThrows(name, exception, _ => action(), description));
    }

    internal Scenario AndThrows(string name, Type exception, Func<Task<object?>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(Definition.AndThrows(name, exception, _ => action(), description));
    }

    /// <summary>Appends a check of what an earlier step gave, typed: the exception a throws step expected, or the value a When returned.
    /// Checks of one type pair in order with the steps of their When and Then block that give it.</summary>
    public Scenario Then<TValue>(string name, Action<TValue> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.Then(StepValues.Check<object, TValue>(name, (_, value) => check(value), description)));
    }

    /// <summary>Appends an asynchronous check of what an earlier step gave, typed: the exception a throws step expected, or the value a When returned.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario Then<TValue>(string name, Func<TValue, Task> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.Then(StepValues.Check<object, TValue>(name, (_, value) => check(value), description)));
    }

    internal Scenario Then<TValue>(string name, Action<TValue, CancellationToken> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.Then(StepValues.Check<object, TValue>(name, (_, value, token) => check(value, token), description)));
    }

    /// <summary>Appends an asynchronous typed check of what an earlier step gave, that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario Then<TValue>(string name, Func<TValue, CancellationToken, Task> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.Then(StepValues.Check<object, TValue>(name, (_, value, token) => check(value, token), description)));
    }

    /// <summary>After Then, adds another check of what an earlier step gave, typed.</summary>
    public Scenario And<TValue>(string name, Action<TValue> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.And(StepValues.Check<object, TValue>(name, (_, value) => check(value), description)));
    }

    /// <summary>After Then, adds another asynchronous check of what an earlier step gave, typed.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario And<TValue>(string name, Func<TValue, Task> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.And(StepValues.Check<object, TValue>(name, (_, value) => check(value), description)));
    }

    internal Scenario And<TValue>(string name, Action<TValue, CancellationToken> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.And(StepValues.Check<object, TValue>(name, (_, value, token) => check(value, token), description)));
    }

    /// <summary>After Then, adds another asynchronous typed check of what an earlier step gave, that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario And<TValue>(string name, Func<TValue, CancellationToken, Task> check, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new(Definition.And(StepValues.Check<object, TValue>(name, (_, value, token) => check(value, token), description)));
    }

    /// <summary>Links this scenario to where its work is tracked, such as a ticket.</summary>
    public Scenario Reference(string provider, string key, Uri? url = null) =>
        new(Definition.Reference(provider, key, url));

    internal Task<Result<AssuranceRunResult, AssuranceExecutionError>> RunAsync(CancellationToken cancellationToken = default) =>
        Definition.RunAsync(cancellationToken);
}
