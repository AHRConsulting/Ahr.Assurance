using System.Collections.Immutable;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting;

/// <summary>A problem met while running a verification or delivering its evidence.</summary>
/// <param name="Code">The kind of problem, such as <c>execution-error</c> or <c>report-sink-error</c>.</param>
/// <param name="Message">What happened.</param>
/// <param name="ExceptionType">The full name of the exception type behind it, when there is one.</param>
/// <param name="DetailsWithheld"><see langword="true"/> when operational details were left out, so the message is a generic one.</param>
public sealed record ReportDiagnostic(string Code, string Message, string? ExceptionType = null, bool DetailsWithheld = false);

/// <summary>Run evidence, which may be partial when execution diagnostics are present.</summary>
public sealed record AssuranceReport(
    AssuranceRunResult Run,
    ImmutableArray<ReportDiagnostic> Diagnostics,
    ImmutableArray<AssuranceArtifact> Artifacts = default)
{
    internal ImmutableArray<BusinessReportGroup> BusinessGroups => BusinessReportGrouping.Create(Run);
}

/// <summary>A file an evidence sink wrote for a verification.</summary>
/// <param name="Name">The name of the sink that wrote it, such as <c>JSON</c> or <c>HTML</c>.</param>
/// <param name="Location">The full path of the file.</param>
public sealed record AssuranceArtifact(string Name, string Location);

internal interface IAssuranceReporter
{
    Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken = default);
}
