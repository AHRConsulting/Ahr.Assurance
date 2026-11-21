using System.Collections.Immutable;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting;

internal interface IReportFormat
{
    string Name { get; }

    Task<ReportFormatResult> WriteAsync(ReportFormatContext context, CancellationToken cancellationToken = default);
}

internal sealed class ReportFormatContext
{
    public required ImmutableArray<ReportFormatInput> Inputs { get; init; }

    public required string OutputDirectory { get; init; }

    public string? Title { get; init; }

    public string? DefaultTitle { get; init; }

    public ImmutableArray<string> Contexts { get; init; } = [];

    public ImmutableArray<string> ExcludedContexts { get; init; } = [];

    public bool Developer { get; init; }

    public bool IncludeDiagnosticDetails { get; init; }

    public bool Shows(ScenarioResult scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var id = scenario.Business?.Id;
        return IsSelected(id) && !IsExcluded(id);
    }

    private bool IsSelected(string? id) => Contexts.IsDefaultOrEmpty || (id is not null && Contexts.Contains(id, StringComparer.Ordinal));

    private bool IsExcluded(string? id) => id is not null && !ExcludedContexts.IsDefaultOrEmpty && ExcludedContexts.Contains(id, StringComparer.Ordinal);
}

internal sealed class ReportFormatInput
{
    public required AssuranceResults Results { get; init; }

    public string? Project { get; init; }

    internal TestRunSummary? Tests { get; init; }
}

internal sealed class ReportFormatResult
{
    public required string Path { get; init; }

    public ImmutableArray<ReportDiagnostic> Diagnostics { get; init; } = [];
}
