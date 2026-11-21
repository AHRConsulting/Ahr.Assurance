using Ahr.Assurance.Core;

namespace Ahr.Assurance.Execution;

/// <summary>
/// Optional authoring helper for reusable steps over a typed context.
/// Definitions receive fresh execution state from the runner; do not capture live contexts in shared steps.
/// </summary>
public class ScenarioSteps<TContext>
    where TContext : notnull
{
    /// <summary>Lets a derived class group reusable steps over <typeparamref name="TContext"/>.</summary>
    protected ScenarioSteps()
    {
    }

    /// <summary>Creates reusable setup or operation work for Given/When and their action And steps.</summary>
    protected static ScenarioAction<TContext> CreateActionStep(
        string name, Action<TContext> execute, string? description = null) =>
        ScenarioAction<TContext>.Synchronous(name, execute, description);

    /// <summary>Creates reusable asynchronous setup or operation work for Given/When and their action And steps.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioAction<TContext> CreateActionStep(
        string name, Func<TContext, Task> execute, string? description = null) =>
        ScenarioAction<TContext>.Asynchronous(name, execute, description);

    /// <summary>Creates reusable asynchronous setup or operation work that takes the run's cancellation token, for Given/When and their action And steps.</summary>
    protected static ScenarioAction<TContext> CreateActionStep(
        string name, Func<TContext, CancellationToken, Task> execute, string? description = null) =>
        new(name, execute, description);

    private protected static ScenarioAction<TContext> CreateActionStep(
        string name, Action<TContext, CancellationToken> execute, string? description = null) =>
        ScenarioAction<TContext>.Synchronous(name, execute, description);

    /// <summary>Creates operation work that returns a value. Checks after it take the value, typed: a <c>Then</c> lambda that takes a
    /// parameter of its type, or a step from <see cref="CreateReturnedStep{TResult}(string, Action{TContext, TResult}, string?)"/>.</summary>
    protected static ScenarioAction<TContext> CreateActionStep<TResult>(
        string name, Func<TContext, TResult> execute, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return StepValues.Returning<TContext, TResult>(name, (context, _) => Task.FromResult(execute(context)), description);
    }

    /// <summary>Creates asynchronous operation work that returns a value. Checks after it take the value, typed.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    protected static ScenarioAction<TContext> CreateActionStep<TResult>(
        string name, Func<TContext, Task<TResult>> execute, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return StepValues.Returning<TContext, TResult>(name, (context, _) => execute(context), description);
    }

    /// <summary>Creates asynchronous operation work that takes the run's cancellation token and returns a value. Checks after it take the value, typed.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioAction<TContext> CreateActionStep<TResult>(
        string name, Func<TContext, CancellationToken, Task<TResult>> execute, string? description = null) =>
        StepValues.Returning(name, execute, description);

    /// <summary>Creates a check from ordinary assertions, from any test framework or assertion library.</summary>
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Action<TContext> assertion, string? description = null) =>
        ScenarioExpectation<TContext>.Assertion(name, assertion, description);

    /// <summary>Creates an asynchronous check from ordinary assertions, from any test framework or assertion library.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Func<TContext, Task> assertion, string? description = null) =>
        ScenarioExpectation<TContext>.Assertion(name, assertion, description);

    /// <summary>Creates an asynchronous check from ordinary assertions that takes the run's cancellation token.</summary>
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Func<TContext, CancellationToken, Task> assertion, string? description = null) =>
        ScenarioExpectation<TContext>.Assertion(name, assertion, description);

    private protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Action<TContext, CancellationToken> assertion, string? description = null) =>
        ScenarioExpectation<TContext>.Assertion(name, assertion, description);

    /// <summary>Creates a check that returns structured evidence as an <see cref="ExpectationResult"/>, the same as
    /// <see cref="CreateExpectationStep(string, Func{TContext, ExpectationResult}, string?)"/>.</summary>
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Func<TContext, ExpectationResult> evaluate, string? description = null) =>
        CreateExpectationStep(name, evaluate, description);

    /// <summary>Creates an asynchronous check that returns structured evidence as an <see cref="ExpectationResult"/>, the same as
    /// <see cref="CreateExpectationStep(string, Func{TContext, Task{ExpectationResult}}, string?)"/>.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Func<TContext, Task<ExpectationResult>> evaluate, string? description = null) =>
        CreateExpectationStep(name, evaluate, description);

    /// <summary>Creates an asynchronous check that takes the run's cancellation token and returns structured evidence as an
    /// <see cref="ExpectationResult"/>, the same as <see cref="CreateExpectationStep(string, Func{TContext, CancellationToken, Task{ExpectationResult}}, string?)"/>.</summary>
    protected static ScenarioExpectation<TContext> CreateAssertionStep(
        string name, Func<TContext, CancellationToken, Task<ExpectationResult>> evaluate, string? description = null) =>
        CreateExpectationStep(name, evaluate, description);

    /// <summary>Creates a check that gives structured evidence, for Then and its expectation And steps.</summary>
    protected static ScenarioExpectation<TContext> CreateExpectationStep(
        string name, Func<TContext, ExpectationResult> evaluate, string? description = null) =>
        ScenarioExpectation<TContext>.Synchronous(name, evaluate, description);

    /// <summary>Creates an asynchronous check that gives structured evidence, for Then and its expectation And steps.</summary>
    protected static ScenarioExpectation<TContext> CreateExpectationStep(
        string name, Func<TContext, Task<ExpectationResult>> evaluate, string? description = null) =>
        ScenarioExpectation<TContext>.Asynchronous(name, evaluate, description);

    /// <summary>Creates an asynchronous check that takes the run's cancellation token and gives structured evidence, for Then and its expectation And steps.</summary>
    protected static ScenarioExpectation<TContext> CreateExpectationStep(
        string name, Func<TContext, CancellationToken, Task<ExpectationResult>> evaluate, string? description = null) =>
        new(name, evaluate, description);

    /// <summary>Creates operation work that must throw exactly <typeparamref name="TException"/>, as <c>Assert.Throws</c> checks. Anything
    /// else, or nothing, fails the step and stops the scenario. Steps from <see cref="CreateThrownStep{TException}(string, Action{TContext, TException}, string?)"/>
    /// then take the exception, typed.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep<TException>(string name, Action<TContext> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext, TException>(name, (context, _) =>
        {
            execute(context);
            return Task.CompletedTask;
        }, description);
    }

    /// <summary>Creates asynchronous operation work that must throw exactly <typeparamref name="TException"/>. Anything else, or nothing, fails the
    /// step and stops the scenario.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioAction<TContext> CreateThrowsStep<TException>(string name, Func<TContext, Task> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext, TException>(name, (context, _) => execute(context), description);
    }

    /// <summary>Creates asynchronous operation work that takes the run's cancellation token and must throw exactly <typeparamref name="TException"/>. Anything else, or
    /// nothing, fails the step and stops the scenario.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep<TException>(
        string name, Func<TContext, CancellationToken, Task> execute, string? description = null)
        where TException : Exception =>
        ExpectedExceptions.Step<TContext, TException>(name, execute, description);

    /// <summary>Creates work that returns a value, such as a property read, and must throw exactly <typeparamref name="TException"/>.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep<TException>(string name, Func<TContext, object?> execute, string? description = null)
        where TException : Exception =>
        ExpectedExceptions.Returning<TContext, TException>(name, execute, description);

    /// <summary>Creates asynchronous work that returns a value and must throw exactly <typeparamref name="TException"/>.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep<TException>(string name, Func<TContext, Task<object?>> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext, TException>(name, (context, _) => execute(context), description);
    }

    /// <summary>Creates operation work that must throw <typeparamref name="TException"/> or a type derived from it, as <c>Assert.ThrowsAny</c> checks. Anything
    /// else, or nothing, fails the step and stops the scenario. Steps from <see cref="CreateThrownStep{TException}(string, Action{TContext, TException}, string?)"/>
    /// then take the exception, typed.</summary>
    protected static ScenarioAction<TContext> CreateThrowsAnyStep<TException>(string name, Action<TContext> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) =>
        {
            execute(context);
            return Task.CompletedTask;
        }, description);
    }

    /// <summary>Creates asynchronous operation work that must throw <typeparamref name="TException"/> or a type derived from it. Anything else, or
    /// nothing, fails the step and stops the scenario.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioAction<TContext> CreateThrowsAnyStep<TException>(string name, Func<TContext, Task> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => execute(context), description);
    }

    /// <summary>Creates asynchronous operation work that takes the run's cancellation token and must throw <typeparamref name="TException"/> or a type derived from it.
    /// Anything else, or nothing, fails the step and stops the scenario.</summary>
    protected static ScenarioAction<TContext> CreateThrowsAnyStep<TException>(
        string name, Func<TContext, CancellationToken, Task> execute, string? description = null)
        where TException : Exception =>
        ExpectedExceptions.StepAny<TContext, TException>(name, execute, description);

    /// <summary>Creates work that returns a value, such as a property read, and must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    protected static ScenarioAction<TContext> CreateThrowsAnyStep<TException>(string name, Func<TContext, object?> execute, string? description = null)
        where TException : Exception =>
        ExpectedExceptions.ReturningAny<TContext, TException>(name, execute, description);

    /// <summary>Creates asynchronous work that returns a value and must throw <typeparamref name="TException"/> or a type derived from it.</summary>
    protected static ScenarioAction<TContext> CreateThrowsAnyStep<TException>(string name, Func<TContext, Task<object?>> execute, string? description = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.StepAny<TContext, TException>(name, (context, _) => execute(context), description);
    }

    /// <summary>Creates operation work that must throw exactly <paramref name="exception"/>, a type known only when the test runs, such as a
    /// theory row's. Later checks take the exception as that type or a base of it.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep(string name, Type exception, Action<TContext> execute, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext>(name, exception, (context, _) =>
        {
            execute(context);
            return Task.CompletedTask;
        }, description);
    }

    /// <summary>Creates asynchronous operation work that must throw exactly <paramref name="exception"/>, a type known only when the test runs.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioAction<TContext> CreateThrowsStep(string name, Type exception, Func<TContext, Task> execute, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext>(name, exception, (context, _) => execute(context), description);
    }

    /// <summary>Creates asynchronous operation work that takes the run's cancellation token and must throw exactly <paramref name="exception"/>, a type known only when
    /// the test runs.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep(
        string name, Type exception, Func<TContext, CancellationToken, Task> execute, string? description = null) =>
        ExpectedExceptions.Step(name, exception, execute, description);

    /// <summary>Creates work that returns a value, such as a constructor call, and must throw exactly <paramref name="exception"/>.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep(string name, Type exception, Func<TContext, object?> execute, string? description = null) =>
        ExpectedExceptions.Returning(name, exception, execute, description);

    /// <summary>Creates asynchronous work that returns a value and must throw exactly <paramref name="exception"/>.</summary>
    protected static ScenarioAction<TContext> CreateThrowsStep(string name, Type exception, Func<TContext, Task<object?>> execute, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return ExpectedExceptions.Step<TContext>(name, exception, (context, _) => execute(context), description);
    }

    /// <summary>Creates a check of the <typeparamref name="TException"/> an earlier throws step expected.</summary>
    protected static ScenarioExpectation<TContext> CreateThrownStep<TException>(string name, Action<TContext, TException> check, string? description = null)
        where TException : Exception =>
        StepValues.Check(name, check, description);

    /// <summary>Creates an asynchronous check of the <typeparamref name="TException"/> an earlier throws step expected.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioExpectation<TContext> CreateThrownStep<TException>(string name, Func<TContext, TException, Task> check, string? description = null)
        where TException : Exception =>
        StepValues.Check(name, check, description);

    private protected static ScenarioExpectation<TContext> CreateThrownStep<TException>(
        string name, Action<TContext, TException, CancellationToken> check, string? description = null)
        where TException : Exception =>
        StepValues.Check(name, check, description);

    /// <summary>Creates an asynchronous check of the <typeparamref name="TException"/> an earlier throws step expected that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioExpectation<TContext> CreateThrownStep<TException>(
        string name, Func<TContext, TException, CancellationToken, Task> check, string? description = null)
        where TException : Exception =>
        StepValues.Check(name, check, description);

    /// <summary>Creates a check of the <typeparamref name="TResult"/> an earlier step returned.</summary>
    protected static ScenarioExpectation<TContext> CreateReturnedStep<TResult>(string name, Action<TContext, TResult> check, string? description = null) =>
        StepValues.Check(name, check, description);

    /// <summary>Creates an asynchronous check of the <typeparamref name="TResult"/> an earlier step returned.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioExpectation<TContext> CreateReturnedStep<TResult>(string name, Func<TContext, TResult, Task> check, string? description = null) =>
        StepValues.Check(name, check, description);

    private protected static ScenarioExpectation<TContext> CreateReturnedStep<TResult>(
        string name, Action<TContext, TResult, CancellationToken> check, string? description = null) =>
        StepValues.Check(name, check, description);

    /// <summary>Creates an asynchronous check of the <typeparamref name="TResult"/> an earlier step returned that takes the run's cancellation token.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    protected static ScenarioExpectation<TContext> CreateReturnedStep<TResult>(
        string name, Func<TContext, TResult, CancellationToken, Task> check, string? description = null) =>
        StepValues.Check(name, check, description);
}
