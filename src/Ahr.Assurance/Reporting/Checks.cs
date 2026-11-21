using System.Collections.Immutable;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class Checks
{
    internal static Result That(bool condition, string message) => condition ? Result.Success() : message.ToFailure();

    internal static Result That<T>(bool condition, T state, Func<T, string> message) => condition ? Result.Success() : message(state).ToFailure();

    internal static Result Text(string? value, string name) => string.IsNullOrWhiteSpace(value) ? Required(name) : Result.Success();

    internal static Result Present(object? value, string name) => value is null ? Required(name) : Result.Success();

    private static Result Required(string name) => $"'{name}' is required.".ToFailure();

    internal static Result All<T>(T state, params ReadOnlySpan<Func<T, Result>> checks)
    {
        foreach (Func<T, Result> check in checks)
        {
            Result result = check(state);
            if (result.IsFailure)
            {
                return result;
            }
        }
        return Result.Success();
    }

    internal static Result Each<T, TState>(ImmutableArray<T> items, TState state, Func<T, TState, Result> check)
    {
        foreach (T item in items)
        {
            Result result = check(item, state);
            if (result.IsFailure)
            {
                return result;
            }
        }
        return Result.Success();
    }

    internal static Result Each<T, TState>(IEnumerable<T> items, TState state, Func<T, TState, Result> check)
    {
        foreach (T item in items)
        {
            Result result = check(item, state);
            if (result.IsFailure)
            {
                return result;
            }
        }
        return Result.Success();
    }
}
