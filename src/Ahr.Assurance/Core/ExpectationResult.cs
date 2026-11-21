namespace Ahr.Assurance.Core;

/// <summary>The verdict on a step, scenario or run.</summary>
public enum AssuranceStatus
{
    /// <summary>The step ran and its check, if any, held.</summary>
    Satisfied,
    /// <summary>The expectation does not hold.</summary>
    Unsatisfied,
    /// <summary>Not checked: the step didn't run, or execution stopped during it. A scenario or run with no results is also not evaluated.</summary>
    NotEvaluated,
    /// <summary>The expectation could not be decided, such as when a step is skipped.</summary>
    Inconclusive,
}

/// <summary>The verdict on one step, with the reason and, for a check, the expected and actual values.</summary>
public sealed record ExpectationResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="status">The verdict.</param>
    /// <param name="explanation">Why the verdict was reached, in plain words.</param>
    /// <param name="expected">The expected value, if any.</param>
    /// <param name="actual">The actual value, if any.</param>
    public ExpectationResult(
        AssuranceStatus status,
        string explanation,
        string? expected = null,
        string? actual = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        Status = status;
        Explanation = explanation;
        Expected = expected;
        Actual = actual;
    }

    /// <summary>The verdict.</summary>
    public AssuranceStatus Status { get; }

    /// <summary>Why the verdict was reached, in plain words.</summary>
    public string Explanation { get; }

    /// <summary>The expected value, or null when there is none.</summary>
    public string? Expected { get; }

    /// <summary>The actual value, or null when there is none.</summary>
    public string? Actual { get; }

    /// <summary>Creates a <see cref="AssuranceStatus.Satisfied"/> result when <paramref name="satisfied"/> is true, and an <see cref="AssuranceStatus.Unsatisfied"/> one otherwise.</summary>
    public static ExpectationResult Check(bool satisfied, string explanation, string expected, string actual) =>
        new(satisfied ? AssuranceStatus.Satisfied : AssuranceStatus.Unsatisfied, explanation, expected, actual);
}
