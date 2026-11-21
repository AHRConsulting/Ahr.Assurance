using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting;

/// <summary>How much detail a verification's text in the test output shows.</summary>
public enum AssuranceTextVerbosity
{
    /// <summary>No text is written to the test output.</summary>
    Off,
    /// <summary>The verdicts and each step's status.</summary>
    Compact,
    /// <summary>Adds each step's intent, evidence, and expected and actual values.</summary>
    Standard,
    /// <summary>Adds the start time and the run's and each step's duration.</summary>
    Detailed,
}

internal sealed class PlainTextReportWriter : IAssuranceReporter
{
    private const string TruncationMarker = "\n[summary truncated]";
    private readonly int _maxCharacters;
    private readonly int _maxFieldLength;

    public PlainTextReportWriter(
        AssuranceTextVerbosity verbosity = AssuranceTextVerbosity.Standard,
        int maxCharacters = 32_000,
        int maxFieldLength = 2_048)
    {
        if (!Enum.IsDefined(verbosity))
        {
            throw new ArgumentOutOfRangeException(nameof(verbosity));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxCharacters, TruncationMarker.Length + 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFieldLength);
        Verbosity = verbosity;
        _maxCharacters = maxCharacters;
        _maxFieldLength = maxFieldLength;
    }

    public AssuranceTextVerbosity Verbosity { get; }

    public string Render(AssuranceReport report) => Render(report, null);

    public string RenderEvidence(VerificationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return Render(evidence.Report, evidence);
    }

    private string Render(AssuranceReport report, VerificationEvidence? evidence)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Run);
        StringBuilder text = new();
        _ = text.Append("Business assurance: ").Append(Field(report.Run.Name)).AppendLine();
        _ = text.Append("Verdict: ").AppendLine(
            ReportStatus(report.Run.Status, report.Diagnostics, !report.Run.Scenarios.IsEmpty));
        _ = text.Append("Scenarios: ").Append(report.Run.Scenarios.Length.ToString(CultureInfo.InvariantCulture)).AppendLine();
        if (evidence is not null)
        {
            _ = text.Append("Verification: ").AppendLine(Field(evidence.VerificationId))
                .Append("Correlation: ").Append(Field(evidence.CorrelationId)).Append(" (source: ").Append(Field(evidence.CorrelationSource)).AppendLine(")")
                .Append("Execution: ").AppendLine(evidence.ExecutionState.ToString());
        }

        if (Verbosity == AssuranceTextVerbosity.Detailed)
        {
            _ = text.Append("Started: ").Append(report.Run.StartedAt.ToString("O", CultureInfo.InvariantCulture))
                .Append(" | Duration: ").Append(report.Run.Duration.ToString("c", CultureInfo.InvariantCulture)).AppendLine();
        }

        foreach (BusinessReportGroup group in report.BusinessGroups)
        {
            if (group.Business.Id is { } id)
            {
                _ = text.Append("Business group: ").Append(Field(group.Business.Name ?? group.Business.Description))
                    .Append(" (").Append(Field(id)).Append("); ")
                    .Append(group.Scenarios.Length).AppendLine(group.Scenarios.Length == 1 ? " associated scenario" : " associated scenarios");
            }
        }

        foreach (ScenarioResult scenario in report.Run.Scenarios)
        {
            _ = text.AppendLine().Append("Scenario: ").AppendLine(Field(scenario.Name));
            if (!scenario.Example.IsDefaultOrEmpty)
            {
                _ = text.Append("Example: ").AppendLine(Field(string.Join(", ", scenario.Example.Select(value => $"{value.Name} = {value.Value}"))));
            }

            if (scenario.Description is not null)
            {
                _ = text.Append("Description: ").AppendLine(Field(scenario.Description));
            }

            _ = text.Append("Verdict: ").AppendLine(scenario.Status.ToString());
            if (scenario.Business is { } business)
            {
                _ = text.Append("Business context: ").AppendLine(Field(business.Description));
                AppendItems(text, "Capability", business.Capabilities);
                AppendItems(text, "Outcome", business.Outcomes);
                AppendItems(text, "Rule", business.Rules);
                AppendItems(text, "Risk", business.Risks);
                AppendItems(text, "Objective", business.Objectives);
                AppendReferences(text, business.References);
            }

            AppendReferences(text, scenario.References);

            foreach (StepResult step in scenario.Steps)
            {
                _ = text.Append(step.Keyword).Append(' ').Append(Field(step.Name))
                    .Append(" — ").AppendLine(step.Evaluation.Status.ToString());
                if (Verbosity != AssuranceTextVerbosity.Compact)
                {
                    if (step.Description is not null)
                    {
                        AppendStepField(text, "Intent", step.Description);
                    }

                    AppendStepField(text, "Evidence", step.Evaluation.Explanation);
                    if (step.Evaluation.Expected is { } expected)
                    {
                        AppendStepField(text, "Expected", expected);
                    }

                    if (step.Evaluation.Actual is { } actual)
                    {
                        AppendStepField(text, "Actual", actual);
                    }
                }

                if (Verbosity == AssuranceTextVerbosity.Detailed)
                {
                    _ = text.Append("  Duration: ").AppendLine(step.Duration.ToString("c", CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (ReportDiagnostic diagnostic in report.Diagnostics)
        {
            _ = text.AppendLine().Append("Diagnostic: ").Append(Field(diagnostic.Code)).Append(": ")
                .AppendLine(Field(diagnostic.Message));
            if (diagnostic.ExceptionType is not null)
            {
                _ = text.Append("  Exception: ").AppendLine(Field(diagnostic.ExceptionType));
            }
        }

        foreach (AssuranceArtifact artifact in report.Artifacts.IsDefault ? [] : report.Artifacts)
        {
            _ = text.Append("Artifact (").Append(Field(artifact.Name)).Append("): ")
                .AppendLine(Field(artifact.Location));
        }

        var rendered = text.ToString();
        return rendered.Length <= _maxCharacters
            ? rendered
            : rendered[..(_maxCharacters - TruncationMarker.Length)] + TruncationMarker;
    }

    public async Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        using StreamWriter writer = new(destination, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteAsync(Render(report).AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void AppendReferences(StringBuilder text, ImmutableArray<ExternalReference> references)
    {
        foreach (ExternalReference reference in references)
        {
            _ = text.Append("Reference: ").Append(Field(reference.Provider)).Append(' ').Append(Field(reference.Key));
            if (reference.Url is { } url)
            {
                _ = text.Append(" (").Append(Field(url.AbsoluteUri)).Append(')');
            }
            _ = text.AppendLine();
        }
    }

    private void AppendItems<TItem>(StringBuilder text, string role, IEnumerable<TItem> items)
        where TItem : BusinessItem
    {
        foreach (TItem item in items)
        {
            _ = text.Append(role).Append(": ")
                .Append(Field(item.Name)).Append(" (").Append(Field(item.Id)).AppendLine(")");
        }
    }

    private void AppendStepField(StringBuilder text, string label, string value)
    {
        _ = text.Append("  ").Append(label).Append(": ");
        var lines = Field(value).ReplaceLineEndings("\n").Split('\n');
        _ = text.AppendLine(lines[0]);
        foreach (var line in lines.AsSpan(1))
        {
            _ = text.Append("    ").AppendLine(line);
        }
    }

    private string Field(string value)
    {
        StringBuilder clean = new(Math.Min(value.Length, _maxFieldLength));
        foreach (var character in value)
        {
            if (char.IsControl(character) && character is not '\n' and not '\r' and not '\t')
            {
                continue;
            }

            if (clean.Length == _maxFieldLength)
            {
                _ = clean.Append(" [truncated]");
                break;
            }

            _ = clean.Append(character);
        }

        return clean.ToString();
    }

    private static string ReportStatus(
        AssuranceStatus status,
        ImmutableArray<ReportDiagnostic> diagnostics,
        bool hasScenarios)
    {
        var hasExecutionDiagnostic = diagnostics.Any(diagnostic =>
            !diagnostic.Code.StartsWith("report-", StringComparison.Ordinal));
        return !hasExecutionDiagnostic
            ? status.ToString()
            : hasScenarios
                ? $"Execution incomplete; {status} verdict and partial evidence"
                : "Execution failed; no complete verdict";
    }
}
