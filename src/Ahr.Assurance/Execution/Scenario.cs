using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

/// <summary>An immutable composition of named, reusable steps over a fresh typed context.</summary>
public sealed class Scenario<TContext>
    where TContext : notnull
{
    private Scenario(
        string id,
        string name,
        BusinessContext? business,
        Func<CancellationToken, Task<TContext>> createContextAsync,
        ImmutableArray<ScenarioStep<TContext>> steps,
        string? description,
        ImmutableArray<ExternalReference> references)
    {
        References = references;
        Id = id;
        Name = name;
        Business = business;
        CreateContextAsync = createContextAsync;
        Steps = steps;
        Description = description;
    }

    /// <summary>The scenario's stable identity in results and reports. It defaults to the name.</summary>
    public string Id { get; }

    /// <summary>The scenario's name, as reports show it.</summary>
    public string Name { get; }

    /// <summary>An optional longer description of the scenario.</summary>
    public string? Description { get; }

    /// <summary>The business context this scenario belongs to, if any.</summary>
    public BusinessContext? Business { get; }

    /// <summary>Where this scenario's work is tracked, in addition to references on its business context.</summary>
    public ImmutableArray<ExternalReference> References { get; }

    internal Func<CancellationToken, Task<TContext>> CreateContextAsync { get; }

    internal ImmutableArray<ScenarioStep<TContext>> Steps { get; }

    internal static Scenario<TContext> Define(
        string name,
        Func<CancellationToken, Task<TContext>> createContextAsync,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(createContextAsync);
        if (id is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
        }

        return new(id ?? name, name, business, createContextAsync, [], description, []);
    }

    internal static Scenario<TContext> Define(
        string name,
        Func<TContext> createContext,
        BusinessContext? business = null,
        string? id = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(createContext);
        return Define(name, cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(createContext());
        }, business, id, description);
    }

    /// <summary>Links this scenario to where its work is tracked, such as a ticket.</summary>
    public Scenario<TContext> Reference(string provider, string key, Uri? url = null)
    {
        ImmutableArray<ExternalReference> references = References.Add(new(provider, key, url));
        ExternalReference.Unique(references).OrThrow(static error => new InvalidOperationException(error.Message));
        return new(Id, Name, Business, CreateContextAsync, Steps, Description, references);
    }

    /// <summary>Appends setup work. Ordinary assertions in it act as preconditions.</summary>
    public Scenario<TContext> Given(ScenarioAction<TContext> action) => AddAction(StepKind.Given, StepKeyword.Given, action);

    /// <summary>Appends setup work over the context. Ordinary assertions in it act as preconditions.</summary>
    public Scenario<TContext> Given(string name, Action<TContext> action, string? description = null) =>
        Given(ScenarioAction<TContext>.Synchronous(name, action, description));

    /// <summary>Appends asynchronous setup work over the context. Ordinary assertions in it act as preconditions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> Given(string name, Func<TContext, Task> action, string? description = null) =>
        Given(ScenarioAction<TContext>.Asynchronous(name, action, description));

    /// <summary>Appends asynchronous setup work over the context that takes the run's cancellation token.</summary>
    public Scenario<TContext> Given(string name, Func<TContext, CancellationToken, Task> action, string? description = null) =>
        Given(new ScenarioAction<TContext>(name, action, description));

    internal Scenario<TContext> Given(string name, Action<TContext, CancellationToken> action, string? description = null) =>
        Given(ScenarioAction<TContext>.Synchronous(name, action, description));

    /// <summary>Appends operation work. Ordinary assertions in it, such as an expected exception, are checks on that step.</summary>
    public Scenario<TContext> When(ScenarioAction<TContext> action) => AddAction(StepKind.When, StepKeyword.When, action);

    /// <summary>Appends operation work over the context.</summary>
    public Scenario<TContext> When(string name, Action<TContext> action, string? description = null) =>
        When(ScenarioAction<TContext>.Synchronous(name, action, description));

    /// <summary>Appends asynchronous operation work over the context.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> When(string name, Func<TContext, Task> action, string? description = null) =>
        When(ScenarioAction<TContext>.Asynchronous(name, action, description));

    /// <summary>Appends asynchronous operation work over the context that takes the run's cancellation token.</summary>
    public Scenario<TContext> When(string name, Func<TContext, CancellationToken, Task> action, string? description = null) =>
        When(new ScenarioAction<TContext>(name, action, description));

    internal Scenario<TContext> When(string name, Action<TContext, CancellationToken> action, string? description = null) =>
        When(ScenarioAction<TContext>.Synchronous(name, action, description));

    /// <summary>Appends operation work that returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    public Scenario<TContext> When<TResult>(string name, Func<TContext, TResult> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(StepValues.Returning<TContext, TResult>(name, (context, _) => Task.FromResult(action(context)), description));
    }

    /// <summary>Appends asynchronous operation work that returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario<TContext> When<TResult>(string name, Func<TContext, Task<TResult>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(StepValues.Returning<TContext, TResult>(name, (context, _) => action(context), description));
    }

    /// <summary>Appends asynchronous operation work that takes the run's cancellation token and returns a value. Checks after it take the value by taking a parameter of its type.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> When<TResult>(string name, Func<TContext, CancellationToken, Task<TResult>> action, string? description = null) =>
        When(StepValues.Returning(name, action, description));

    /// <summary>Appends an assertion-created or structured expectation, not an action component.</summary>
    public Scenario<TContext> Then(ScenarioExpectation<TContext> expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        return AddExpectation(StepKeyword.Then, expectation);
    }

    /// <summary>Appends a check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario<TContext> Then(string name, Func<TContext, ExpectationResult> evaluate, string? description = null) =>
        Then(ScenarioExpectation<TContext>.Synchronous(name, evaluate, description));

    /// <summary>Appends an asynchronous check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario<TContext> Then(string name, Func<TContext, Task<ExpectationResult>> evaluate, string? description = null) =>
            Then(ScenarioExpectation<TContext>.Asynchronous(name, evaluate, description));

    /// <summary>Appends an asynchronous check that takes the run's cancellation token and returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario<TContext> Then(
        string name,
        Func<TContext, CancellationToken, Task<ExpectationResult>> evaluate,
        string? description = null) =>
        Then(new ScenarioExpectation<TContext>(name, evaluate, description));

    /// <summary>Appends a check made of ordinary assertions, from any test framework or assertion library.</summary>
    public Scenario<TContext> Then(string name, Action<TContext> assertion, string? description = null) =>
        Then(ScenarioExpectation<TContext>.Assertion(name, assertion, description));

    /// <summary>Appends an asynchronous check made of ordinary assertions, from any test framework or assertion library.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> Then(string name, Func<TContext, Task> assertion, string? description = null) =>
        Then(ScenarioExpectation<TContext>.Assertion(name, assertion, description));

    /// <summary>Appends an asynchronous check made of ordinary assertions that takes the run's cancellation token.</summary>
    public Scenario<TContext> Then(string name, Func<TContext, CancellationToken, Task> assertion, string? description = null) =>
        Then(ScenarioExpectation<TContext>.Assertion(name, assertion, description));

    internal Scenario<TContext> Then(string name, Action<TContext, CancellationToken> assertion, string? description = null) =>
        Then(ScenarioExpectation<TContext>.Assertion(name, assertion, description));

    /// <summary>Appends action work after Given/When. A mismatched preceding phase throws during composition.</summary>
    public Scenario<TContext> And(ScenarioAction<TContext> action)
    {
        return Steps.IsEmpty || Steps[^1].Kind == StepKind.Then
            ? throw new InvalidOperationException("An action And must follow a Given or When step.")
            : AddAction(Steps[^1].Kind, StepKeyword.And, action);
    }

    /// <summary>Appends an expectation after Then. Other preceding phases throw during composition.</summary>
    public Scenario<TContext> And(ScenarioExpectation<TContext> expectation)
    {
        return Steps.IsEmpty || Steps[^1].Kind != StepKind.Then
            ? throw new InvalidOperationException("An expectation And must follow a Then step.")
            : AddExpectation(StepKeyword.And, expectation);
    }

    internal Scenario<TContext> And(ScenarioAction<TContext> action, ScenarioExpectation<TContext> expectation) =>
        !Steps.IsEmpty && Steps[^1].Kind == StepKind.Then ? And(expectation) : And(action);

    /// <summary>After Then, adds another check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    public Scenario<TContext> And(string name, Func<TContext, ExpectationResult> evaluate, string? description = null) =>
        And(ScenarioExpectation<TContext>.Synchronous(name, evaluate, description));

    /// <summary>After Then, adds another asynchronous check that returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario<TContext> And(string name, Func<TContext, Task<ExpectationResult>> evaluate, string? description = null) =>
            And(ScenarioExpectation<TContext>.Asynchronous(name, evaluate, description));

    /// <summary>After Then, adds another asynchronous check that takes the run's cancellation token and returns structured evidence as an <see cref="ExpectationResult"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> And(
        string name,
        Func<TContext, CancellationToken, Task<ExpectationResult>> evaluate,
        string? description = null) =>
        And(new ScenarioExpectation<TContext>(name, evaluate, description));

    /// <summary>After Given or When, adds more of that work; after Then, adds another check made of ordinary assertions.</summary>
    public Scenario<TContext> And(string name, Action<TContext> step, string? description = null) =>
        And(ScenarioAction<TContext>.Synchronous(name, step, description), ScenarioExpectation<TContext>.Assertion(name, step, description));

    /// <summary>After Given or When, adds more of that work, run asynchronously; after Then, adds another asynchronous check made of ordinary assertions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> And(string name, Func<TContext, Task> step, string? description = null) =>
        And(ScenarioAction<TContext>.Asynchronous(name, step, description), ScenarioExpectation<TContext>.Assertion(name, step, description));

    /// <summary>After Given or When, adds more of that work; after Then, adds another check made of ordinary assertions. The step is asynchronous and takes the run's cancellation token.</summary>
    public Scenario<TContext> And(string name, Func<TContext, CancellationToken, Task> step, string? description = null) =>
        And(new ScenarioAction<TContext>(name, step, description), ScenarioExpectation<TContext>.Assertion(name, step, description));

    internal Scenario<TContext> And(string name, Action<TContext, CancellationToken> step, string? description = null) =>
        And(ScenarioAction<TContext>.Synchronous(name, step, description), ScenarioExpectation<TContext>.Assertion(name, step, description));

    /// <summary>After Given or When, adds more work that returns a value, which the checks after it can take; after Then, adds another check
    /// made of ordinary assertions.</summary>
    public Scenario<TContext> And<TResult>(string name, Func<TContext, TResult> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return And(StepValues.Returning<TContext, TResult>(name, (context, _) => Task.FromResult(step(context)), description),
            ScenarioExpectation<TContext>.Assertion(name, context => step(context), description));
    }

    /// <summary>After Given or When, adds more asynchronous work that returns a value, which the checks after it can take; after Then, adds another
    /// asynchronous check made of ordinary assertions.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    public Scenario<TContext> And<TResult>(string name, Func<TContext, Task<TResult>> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return And(StepValues.Returning<TContext, TResult>(name, (context, _) => step(context), description),
            ScenarioExpectation<TContext>.Assertion(name, context => step(context), description));
    }

    /// <summary>After Given or When, adds more asynchronous work that returns a value, which the checks after it can take; after Then, adds another
    /// asynchronous check made of ordinary assertions. The step takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> And<TResult>(string name, Func<TContext, CancellationToken, Task<TResult>> step, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return And(StepValues.Returning(name, step, description),
            ScenarioExpectation<TContext>.Assertion(name, (context, cancellationToken) => step(context, cancellationToken), description));
    }

    /// <summary>Appends operation work that must throw exactly <typeparamref name="TException"/>, as <c>Assert.Throws</c> checks. Anything
    /// else, or nothing, fails this check and stops the scenario. Later Then and And steps can take the exception, typed.</summary>
    public Scenario<TContext> WhenThrows<TException>(string name, Action<TContext> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext, TException>(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    /// <summary>Async work that must throw exactly <typeparamref name="TException"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> WhenThrows<TException>(string name, Func<TContext, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>Async work that takes the run's cancellation token and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> WhenThrows<TException>(string name, Func<TContext, CancellationToken, Task> action, string? description = null)
        where TException : Exception =>
        When(ExpectedExceptions.Step<TContext, TException>(name, action, description));

    /// <summary>Work that returns a value, such as a property read, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> WhenThrows<TException>(string name, Func<TContext, object?> action, string? description = null)
        where TException : Exception =>
        When(ExpectedExceptions.Returning<TContext, TException>(name, action, description));

    /// <summary>Async work that returns a value, which is not kept, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> WhenThrows<TException>(string name, Func<TContext, Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>After Given or When, adds more work that must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> AndThrows<TException>(string name, Action<TContext> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext, TException>(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    /// <summary>After Given or When, more async work that must throw exactly <typeparamref name="TException"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> AndThrows<TException>(string name, Func<TContext, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>After Given or When, more async work that takes the run's cancellation token and must
    /// throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> AndThrows<TException>(string name, Func<TContext, CancellationToken, Task> action, string? description = null)
        where TException : Exception =>
        And(ExpectedExceptions.Step<TContext, TException>(name, action, description));

    /// <summary>Work that returns a value, such as a property read, and must throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> AndThrows<TException>(string name, Func<TContext, object?> action, string? description = null)
        where TException : Exception =>
        And(ExpectedExceptions.Returning<TContext, TException>(name, action, description));

    /// <summary>After Given or When, more async work that returns a value, which is not kept, and must
    /// throw exactly <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> AndThrows<TException>(string name, Func<TContext, Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>Appends operation work that must
    /// throw <typeparamref name="TException"/> or a type derived from it, as <c>Assert.ThrowsAny</c> checks. Anything
    /// else, or nothing, fails this check and stops the scenario. Later Then and And steps can take the exception as <typeparamref name="TException"/>.</summary>
    public Scenario<TContext> WhenThrowsAny<TException>(string name, Action<TContext> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    /// <summary>Async work that must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> WhenThrowsAny<TException>(string name, Func<TContext, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>Async work that takes the run's cancellation token and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> WhenThrowsAny<TException>(string name, Func<TContext, CancellationToken, Task> action, string? description = null)
        where TException : Exception =>
        When(ExpectedExceptions.StepAny<TContext, TException>(name, action, description));

    /// <summary>Work that returns a value, such as a property read, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> WhenThrowsAny<TException>(string name, Func<TContext, object?> action, string? description = null)
        where TException : Exception =>
        When(ExpectedExceptions.ReturningAny<TContext, TException>(name, action, description));

    /// <summary>Async work that returns a value, which is not kept, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> WhenThrowsAny<TException>(string name, Func<TContext, Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>After Given or When, adds more work that must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> AndThrowsAny<TException>(string name, Action<TContext> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    /// <summary>After Given or When, more async work that must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> AndThrowsAny<TException>(string name, Func<TContext, Task> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => action(context), description));
    }

    /// <summary>After Given or When, more async work that takes the run's cancellation token and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> AndThrowsAny<TException>(string name, Func<TContext, CancellationToken, Task> action, string? description = null)
        where TException : Exception =>
        And(ExpectedExceptions.StepAny<TContext, TException>(name, action, description));

    /// <summary>Work that returns a value, such as a property read, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> AndThrowsAny<TException>(string name, Func<TContext, object?> action, string? description = null)
        where TException : Exception =>
        And(ExpectedExceptions.ReturningAny<TContext, TException>(name, action, description));

    /// <summary>After Given or When, more async work that returns a value, which is not kept, and must
    /// throw <typeparamref name="TException"/> or a type derived from it.</summary>
    public Scenario<TContext> AndThrowsAny<TException>(string name, Func<TContext, Task<object?>> action, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => action(context), description));
    }

    internal Scenario<TContext> WhenThrows(string name, Type exception, Action<TContext> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext>(name, exception, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    internal Scenario<TContext> WhenThrows(string name, Type exception, Func<TContext, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext>(name, exception, (context, _) => action(context), description));
    }

    internal Scenario<TContext> WhenThrows(string name, Type exception, Func<TContext, CancellationToken, Task> action, string? description = null) =>
        When(ExpectedExceptions.Step(name, exception, action, description));

    internal Scenario<TContext> WhenThrows(string name, Type exception, Func<TContext, object?> action, string? description = null) =>
        When(ExpectedExceptions.Returning(name, exception, action, description));

    internal Scenario<TContext> WhenThrows(string name, Type exception, Func<TContext, Task<object?>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return When(ExpectedExceptions.Step<TContext>(name, exception, (context, _) => action(context), description));
    }

    internal Scenario<TContext> AndThrows(string name, Type exception, Action<TContext> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext>(name, exception, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        }, description));
    }

    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    internal Scenario<TContext> AndThrows(string name, Type exception, Func<TContext, Task> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext>(name, exception, (context, _) => action(context), description));
    }

    internal Scenario<TContext> AndThrows(string name, Type exception, Func<TContext, CancellationToken, Task> action, string? description = null) =>
        And(ExpectedExceptions.Step(name, exception, action, description));

    internal Scenario<TContext> AndThrows(string name, Type exception, Func<TContext, object?> action, string? description = null) =>
        And(ExpectedExceptions.Returning(name, exception, action, description));

    internal Scenario<TContext> AndThrows(string name, Type exception, Func<TContext, Task<object?>> action, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return And(ExpectedExceptions.Step<TContext>(name, exception, (context, _) => action(context), description));
    }

    /// <summary>Appends a check of what an earlier step gave, typed: the exception a throws step expected, or the value a When returned.
    /// Checks of one type pair in order with the steps of their When and Then block that give it.</summary>
    public Scenario<TContext> Then<TValue>(string name, Action<TContext, TValue> check, string? description = null) =>
        Then(StepValues.Check(name, check, description));

    /// <summary>Appends an asynchronous check of what an earlier step gave, typed: the exception a throws step expected, or the value a When returned.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> Then<TValue>(string name, Func<TContext, TValue, Task> check, string? description = null) =>
        Then(StepValues.Check(name, check, description));

    internal Scenario<TContext> Then<TValue>(string name, Action<TContext, TValue, CancellationToken> check, string? description = null) =>
        Then(StepValues.Check(name, check, description));

    /// <summary>Appends an asynchronous typed check of what an earlier step gave, that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> Then<TValue>(string name, Func<TContext, TValue, CancellationToken, Task> check, string? description = null) =>
        Then(StepValues.Check(name, check, description));

    /// <summary>After Then, adds another check of what an earlier step gave, typed.</summary>
    public Scenario<TContext> And<TValue>(string name, Action<TContext, TValue> check, string? description = null) =>
        And(StepValues.Check(name, check, description));

    /// <summary>After Then, adds another asynchronous check of what an earlier step gave, typed.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> And<TValue>(string name, Func<TContext, TValue, Task> check, string? description = null) =>
        And(StepValues.Check(name, check, description));

    internal Scenario<TContext> And<TValue>(string name, Action<TContext, TValue, CancellationToken> check, string? description = null) =>
        And(StepValues.Check(name, check, description));

    /// <summary>After Then, adds another asynchronous typed check of what an earlier step gave, that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public Scenario<TContext> And<TValue>(string name, Func<TContext, TValue, CancellationToken, Task> check, string? description = null) =>
        And(StepValues.Check(name, check, description));

    internal Result Validate() =>
        Checks.All(this,
            static scenario => Checks.That(scenario.Steps.Any(static step => step.Kind == StepKind.When), scenario,
                static item => $"Scenario '{item.Id}' requires a When step."),
            static scenario => Checks.That(scenario.Steps[0].Kind != StepKind.Then, scenario,
                static item => $"Scenario '{item.Id}' must start with a Given or When step."),
            static scenario => Checks.That(scenario.InOrder(), scenario,
                static item => $"Scenario '{item.Id}' must order steps as Given, When, then Then; only a When may follow a Then."),
            static scenario => Checks.That(scenario.EndsWithCheck(), scenario,
                static item => $"Scenario '{item.Id}' must end with a Then step, or a throws step, that checks its last When."),
            static scenario => scenario.UnpairedCheck().Match(static problem => problem.ToFailure(), static () => Result.Success()));

    private bool InOrder()
    {
        for (var index = 1; index < Steps.Length; index++)
        {
            if (!MayFollow(Steps[index - 1].Kind, Steps[index].Kind))
            {
                return false;
            }
        }
        return true;
    }

    private Option<string> UnpairedCheck() =>
        TakesAnyValue() ? Blocks().Select(MismatchedChecks).Values().FirstOrNone() : Option<string>.None;

    private IEnumerable<(int Start, int Checks, int End)> Blocks()
    {
        var start = 0;
        while (start < Steps.Length)
        {
            var checks = start;
            while (checks < Steps.Length && Steps[checks].Kind != StepKind.Then)
            {
                checks++;
            }

            var end = checks;
            while (end < Steps.Length && Steps[end].Kind == StepKind.Then)
            {
                end++;
            }

            yield return (start, checks, end);
            start = end;
        }
    }

    private Option<string> MismatchedChecks((int Start, int Checks, int End) block) =>
        Steps[block.Checks..block.End].Where(step => step.Takes is not null).GroupBy(step => step.Takes!)
            .Select(taking => Mismatch(block, taking))
            .Values()
            .FirstOrNone();

    private Option<string> Mismatch((int Start, int Checks, int End) block, IGrouping<Type, ScenarioStep<TContext>> taking)
    {
        ScenarioStep<TContext>[] giving = [.. Givers(block.Start, block.Checks, taking.Key).Select(index => Steps[index])];
        if (giving.Length == 0)
        {
            return Option<string>.Some(NothingToRead(block.Start, block.Checks, taking.First().Name, taking.Key));
        }

        if (giving.Length == 1 || giving.Length == taking.Count())
        {
            return Option<string>.None;
        }

        var gives = StepValues.Giving(taking.Key);
        var give = StepValues.Give(taking.Key);
        return Option<string>.Some($"Scenario '{Id}' pairs each check of {TypeNames.Of(taking.Key)} with a step before it that {gives} one, in order, but "
            + $"{Counted(giving.Length, $"step {gives}", $"steps {give}")} one ({Names(giving)}) and {Counted(taking.Count(), "check takes", "checks take")} one "
            + $"({Names(taking)}). Write one check for each, or check them in separate When and Then blocks.");
    }

    private string NothingToRead(int start, int checks, string check, Type takes)
    {
        return NullableGiver(start, checks, takes).Match(
            nullable => StepValues.NullableInItsBlock(check, takes, nullable.Name),
            () => EarlierBlockGiver(start, check, takes));
    }

    private string EarlierBlockGiver(int start, string check, Type takes) =>
        Steps[..start].LastOrNone(step => Gives(step, takes)).Match(
            giver => StepValues.NoStepInItsBlock(check, takes, giver.Name),
            () => StepValues.NoEarlierStep(check, takes));

    private Option<ScenarioStep<TContext>> NullableGiver(int start, int end, Type takes) =>
        Option<Type>.Some(takes).Where(IsPlainStruct)
            .Bind(type => Steps[start..end].FirstOrNone(step => step.Gives == typeof(Nullable<>).MakeGenericType(type)));

    private static bool IsPlainStruct(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null;

    private List<int> Givers(int start, int end, Type takes)
    {
        List<int> exact = [];
        List<int> derived = [];
        for (var index = start; index < end; index++)
        {
            if (Steps[index].Gives == takes)
            {
                exact.Add(index);
            }
            else if (Gives(Steps[index], takes))
            {
                derived.Add(index);
            }
        }

        if (exact.Count > 0)
        {
            return exact;
        }

        return derived;
    }

    private bool TakesAnyValue()
    {
        foreach (ScenarioStep<TContext> step in Steps)
        {
            if (step.Takes is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static string Counted(int count, string one, string many)
    {
        if (count == 1)
        {
            return $"1 {one}";
        }

        return $"{count} {many}";
    }

    private static string Names(IEnumerable<ScenarioStep<TContext>> steps) => string.Join(", ", steps.Select(step => $"'{step.Name}'"));

    private static bool Gives(ScenarioStep<TContext> step, Type takes) => step.Gives is { } gives && takes.IsAssignableFrom(gives);

    private Option<int> PairFor(Type takes)
    {
        var index = Steps.Length - 1;
        var earlierChecks = 0;
        for (; index >= 0 && Steps[index].Kind == StepKind.Then; index--)
        {
            if (Steps[index].Takes == takes)
            {
                earlierChecks++;
            }
        }

        var actionsEnd = index + 1;
        while (index >= 0 && Steps[index].Kind != StepKind.Then)
        {
            index--;
        }

        List<int> giving = Givers(index + 1, actionsEnd, takes);

        if (giving.Count == 1)
        {
            return Option<int>.Some(PlaceAmongGivers(giving[0]));
        }

        return earlierChecks < giving.Count ? Option<int>.Some(PlaceAmongGivers(giving[earlierChecks])) : Option<int>.None;
    }

    private int PlaceAmongGivers(int stepIndex) => Steps.Take(stepIndex).Count(step => step.Gives is not null);

    private bool EndsWithCheck() => Steps[^1].Kind == StepKind.Then || (Steps[^1].Kind == StepKind.When && Steps[^1].Checks);

    private static bool MayFollow(StepKind previous, StepKind next) =>
        next >= previous || (previous == StepKind.Then && next == StepKind.When);

    private Scenario<TContext> AddExpectation(StepKeyword keyword, ScenarioExpectation<TContext> expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        return Add(new(expectation.Name, StepKind.Then, Paired(expectation), expectation.Description, keyword)
        {
            Parameters = expectation.Parameters,
            Takes = expectation.Takes
        });
    }

    private Func<TContext, CancellationToken, Task<ExpectationResult>> Paired(ScenarioExpectation<TContext> expectation)
    {
        if (expectation is { PairedWith: { } pairedWith, Takes: { } takes })
        {
            return pairedWith(PairFor(takes));
        }

        return expectation.EvaluateAsync;
    }

    private Scenario<TContext> AddAction(StepKind kind, StepKeyword keyword, ScenarioAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (kind == StepKind.Given)
        {
            action = action.WithoutResult();
        }

        return Add(new(action.Name, kind, async (context, cancellationToken) =>
        {
            try
            {
                await (action.ExecuteAsync(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(action.Name)).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }

            return new(AssuranceStatus.Satisfied, "Step completed.");
        }, action.Description, keyword)
        {
            Parameters = action.Parameters,
            Gives = action.Gives,
            Checks = action.Checks
        });
    }

    private Scenario<TContext> Add(ScenarioStep<TContext> step) =>
        new(Id, Name, Business, CreateContextAsync, Steps.Add(step), Description, References);
}

internal sealed record ScenarioStep<TContext>(
    string Name,
    StepKind Kind,
    Func<TContext, CancellationToken, Task<ExpectationResult>> ExecuteAsync,
    string? Description,
    StepKeyword Keyword)
{
    internal ImmutableArray<StepParameter> Parameters { get; init; } = [];

    internal Type? Gives { get; init; }

    internal Type? Takes { get; init; }

    internal bool Checks { get; init; }
}
