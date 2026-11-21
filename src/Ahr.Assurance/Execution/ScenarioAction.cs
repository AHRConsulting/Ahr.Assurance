using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Execution;

/// <summary>A named, reusable piece of setup or operation work over a context, for Given/When and their action And steps.</summary>
public sealed class ScenarioAction<TContext>
{
    internal ScenarioAction(string name, Func<TContext, CancellationToken, Task> executeAsync, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(executeAsync);
        Name = name;
        Description = description;
        ExecuteAsync = executeAsync;
    }

    /// <summary>The step's name, as reports show it.</summary>
    public string Name { get; }

    /// <summary>An optional longer description of the step.</summary>
    public string? Description { get; }

    internal Func<TContext, CancellationToken, Task> ExecuteAsync { get; }

    internal ImmutableArray<StepParameter> Parameters { get; init; } = [];

    internal Type? Gives { get; init; }

    internal bool Checks { get; init; }

    internal ScenarioAction<TContext>? Discarding { get; init; }

    internal ScenarioAction<TContext> WithoutResult() => Discarding ?? this;

    internal ScenarioAction<TTarget> For<TTarget>()
        where TTarget : TContext =>
        new(Name, (context, cancellationToken) => ExecuteAsync(context, cancellationToken), Description)
        {
            Parameters = Parameters,
            Gives = Gives,
            Checks = Checks,
            Discarding = Discarding?.For<TTarget>()
        };

    /// <summary>This step, also recording <paramref name="value"/> in the evidence to trace an input. The name defaults to the argument as
    /// written, such as <c>code</c> for <c>WithValue(code)</c>.</summary>
    public ScenarioAction<TContext> WithValue(object? value, [CallerArgumentExpression(nameof(value))] string name = "") =>
        WithParameters([.. Parameters, StepParameter.Of(name, value)]);

    internal ScenarioAction<TContext> WithParameters(ImmutableArray<StepParameter> parameters) =>
        new(Name, ExecuteAsync, Description)
        {
            Parameters = parameters,
            Gives = Gives,
            Checks = Checks,
            Discarding = Discarding?.WithParameters(parameters)
        };

    internal static InvalidOperationException NoTask(string name) => new(
        $"Step '{name}' returned no task to await. A lambda that assigns a task returns it and runs as an async step; "
        + "use a block body, such as () => { task = ...; }, to store the task without awaiting it.");

    internal static ScenarioAction<TContext> Synchronous(
        string name,
        Action<TContext> action,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Synchronous(name, (context, _) => action(context), description);
    }

    internal static ScenarioAction<TContext> Synchronous(
        string name,
        Action<TContext, CancellationToken> action,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(name, (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            action(context, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, description);
    }

    internal static ScenarioAction<TContext> Asynchronous(
        string name,
        Func<TContext, Task> action,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new(name, async (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await (action(context) ?? throw NoTask(name)).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }, description);
    }
}

/// <summary>A named, reusable check over a context, for Then and its expectation And steps.</summary>
public sealed class ScenarioExpectation<TContext>
{
    internal ScenarioExpectation(
        string name,
        Func<TContext, CancellationToken, Task<ExpectationResult>> evaluateAsync,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(evaluateAsync);
        Name = name;
        Description = description;
        EvaluateAsync = evaluateAsync;
    }

    /// <summary>The step's name, as reports show it.</summary>
    public string Name { get; }

    /// <summary>An optional longer description of the step.</summary>
    public string? Description { get; }

    internal Func<TContext, CancellationToken, Task<ExpectationResult>> EvaluateAsync { get; }

    internal ImmutableArray<StepParameter> Parameters { get; init; } = [];

    internal Type? Takes { get; init; }

    internal Func<Option<int>, Func<TContext, CancellationToken, Task<ExpectationResult>>>? PairedWith { get; init; }

    internal static ScenarioExpectation<TContext> Reading<TValue>(string name, Func<Option<int>, ScenarioExpectation<TContext>> create)
    {
        ScenarioExpectation<TContext> unpaired = create(Option<int>.None);
        return new(name, unpaired.EvaluateAsync, unpaired.Description)
        {
            Takes = typeof(TValue),
            PairedWith = paired => create(paired).EvaluateAsync
        };
    }

    internal ScenarioExpectation<TTarget> For<TTarget>()
        where TTarget : TContext =>
        new(Name, (context, cancellationToken) => EvaluateAsync(context, cancellationToken), Description)
        {
            Parameters = Parameters,
            Takes = Takes,
            PairedWith = PairedFor<TTarget>()
        };

    /// <summary>This check, also recording <paramref name="value"/> in the evidence to trace an input. The name defaults to the argument as
    /// written, such as <c>expected</c> for <c>WithValue(expected)</c>.</summary>
    public ScenarioExpectation<TContext> WithValue(object? value, [CallerArgumentExpression(nameof(value))] string name = "") =>
        With([.. Parameters, StepParameter.Of(name, value)]);

    internal ScenarioExpectation<TContext> With(ImmutableArray<StepParameter> parameters) =>
        new(Name, EvaluateAsync, Description)
        {
            Parameters = parameters,
            Takes = Takes,
            PairedWith = PairedWith
        };

    private Func<Option<int>, Func<TTarget, CancellationToken, Task<ExpectationResult>>>? PairedFor<TTarget>()
        where TTarget : TContext
    {
        if (PairedWith is not { } pairedWith)
        {
            return null;
        }

        return paired =>
        {
            Func<TContext, CancellationToken, Task<ExpectationResult>> evaluate = pairedWith(paired);
            return (context, cancellationToken) => evaluate(context, cancellationToken);
        };
    }

    internal static ScenarioExpectation<TContext> Synchronous(
        string name,
        Func<TContext, ExpectationResult> evaluate,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(name, (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExpectationResult result = evaluate(context);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }, description);
    }

    internal static ScenarioExpectation<TContext> Asynchronous(
        string name,
        Func<TContext, Task<ExpectationResult>> evaluate,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new(name, async (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExpectationResult result;
            try
            {
                result = await evaluate(context).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, description);
    }

    internal static ScenarioExpectation<TContext> Assertion(string name, Action<TContext> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return Assertion(name, (context, _) => assertion(context), description);
    }

    internal static ScenarioExpectation<TContext> Assertion(string name, Action<TContext, CancellationToken> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return Assertion(name, (context, cancellationToken) =>
        {
            assertion(context, cancellationToken);
            return Task.CompletedTask;
        }, description);
    }

    [OverloadResolutionPriority(1)]
    internal static ScenarioExpectation<TContext> Assertion(string name, Func<TContext, Task> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return Assertion(name, (context, _) => assertion(context), description);
    }

    internal static ScenarioExpectation<TContext> Assertion(string name, Func<TContext, CancellationToken, Task> assertion, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return new(name, async (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await (assertion(context, cancellationToken) ?? throw ScenarioAction<TContext>.NoTask(name)).ConfigureAwait(false);
            }
            finally
            {
                AttachmentScope.KeepStepContext();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new(AssuranceStatus.Satisfied, "Assertion completed.");
        }, description);
    }
}
