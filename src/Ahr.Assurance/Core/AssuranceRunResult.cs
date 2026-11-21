using System.Collections.Immutable;

namespace Ahr.Assurance.Core;

/// <summary>The phase a step belongs to.</summary>
public enum StepKind
{
    /// <summary>A setup step.</summary>
    Given,
    /// <summary>An action step.</summary>
    When,
    /// <summary>A check step.</summary>
    Then,
}

/// <summary>The keyword a step is shown with in reports.</summary>
public enum StepKeyword
{
    /// <summary>Shown as Given.</summary>
    Given,
    /// <summary>Shown as When.</summary>
    When,
    /// <summary>Shown as Then.</summary>
    Then,
    /// <summary>Shown as And, continuing the previous step's phase.</summary>
    And,
}

/// <summary>The result of one step in a scenario.</summary>
/// <param name="Name">The step's name.</param>
/// <param name="Kind">The phase the step belongs to.</param>
/// <param name="Evaluation">The step's verdict.</param>
/// <param name="Duration">How long the step took to run.</param>
/// <param name="Description">An optional longer description of the step.</param>
/// <param name="AuthoredKeyword">The keyword the step was written with, or null to use the one that matches its phase.</param>
public sealed record StepResult(
    string Name,
    StepKind Kind,
    ExpectationResult Evaluation,
    TimeSpan Duration,
    string? Description = null,
    StepKeyword? AuthoredKeyword = null)
{
    internal bool IsExecutionInterrupted { get; init; }

    /// <summary>The values a step built from a value was given, by the names of its callback's parameters.</summary>
    public ImmutableArray<StepParameter> Parameters { get; init; } = [];

    /// <summary>The keyword the step is shown with: the one it was written with, or else the one that matches its phase.</summary>
    public StepKeyword Keyword => AuthoredKeyword ?? Kind switch
    {
        StepKind.Given => StepKeyword.Given,
        StepKind.When => StepKeyword.When,
        StepKind.Then => StepKeyword.Then,
        _ => throw new InvalidOperationException("Unknown step phase."),
    };
}

/// <summary>An input a step records for tracing, through <c>WithValue</c>, written with the invariant culture.</summary>
public sealed record StepParameter(string Name, string Value)
{
    internal static StepParameter Of(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name, Text(value));
    }

    private static string Text(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return text;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value is System.Collections.IEnumerable items)
        {
            return "[" + string.Join(", ", items.Cast<object?>().Select(Text)) + "]";
        }

        return value.ToString() ?? "";
    }
}

/// <summary>The result of one scenario.</summary>
/// <param name="Id">The scenario's stable ID.</param>
/// <param name="Name">The scenario's readable name.</param>
/// <param name="Business">The business context the scenario protects, or null when it has none.</param>
/// <param name="Steps">The results of the scenario's steps, in order.</param>
/// <param name="Description">An optional longer description of the scenario.</param>
public sealed record ScenarioResult(
    string Id,
    string Name,
    BusinessContext? Business,
    ImmutableArray<StepResult> Steps,
    string? Description = null)
{
    /// <summary>The scenario's verdict: unsatisfied if any step is, otherwise not evaluated if any step was not checked or there are no steps, otherwise inconclusive if any step is, otherwise satisfied.</summary>
    public AssuranceStatus Status => Verdict.Aggregate(Steps.Select(step => step.Evaluation.Status));

    /// <summary>References declared on this scenario, in addition to those on its business context.</summary>
    public ImmutableArray<ExternalReference> References { get; init; } = [];

    /// <summary>For one row of a parameterised test, the scenario's ID without the row; rows that share it are examples of one scenario.</summary>
    public string? OutlineId { get; init; }

    /// <summary>For one row of a parameterised test, the row's arguments by the test method's parameter names.</summary>
    public ImmutableArray<StepParameter> Example { get; init; } = [];
}

/// <summary>The result of a run of scenarios.</summary>
/// <param name="Name">The run's name.</param>
/// <param name="StartedAt">When the run started.</param>
/// <param name="Duration">How long the run took.</param>
/// <param name="Scenarios">The results of the scenarios in the run.</param>
public sealed record AssuranceRunResult(
    string Name,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    ImmutableArray<ScenarioResult> Scenarios)
{
    /// <summary>The run's verdict, combined from its scenarios the same way a scenario's verdict is combined from its steps.</summary>
    public AssuranceStatus Status => Verdict.Aggregate(Scenarios.Select(scenario => scenario.Status));
}

internal static class Verdict
{
    internal static AssuranceStatus Aggregate(IEnumerable<AssuranceStatus> statuses)
    {
        AssuranceStatus[] values = [.. statuses];
        return values.Contains(AssuranceStatus.Unsatisfied)
            ? AssuranceStatus.Unsatisfied
            : values.Contains(AssuranceStatus.NotEvaluated) || values.Length == 0
            ? AssuranceStatus.NotEvaluated
            : values.Contains(AssuranceStatus.Inconclusive)
            ? AssuranceStatus.Inconclusive
            : AssuranceStatus.Satisfied;
    }
}
