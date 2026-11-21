using System.Collections.Immutable;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting;

internal sealed record VerificationResult(AssuranceReport Report, ImmutableArray<ReportDiagnostic> Failures, Exception? Cause = null)
{
    public AssuranceRunResult Run => Report.Run;
    public string Name => Run.Name;
    public AssuranceStatus Status => Run.Status;
    public ImmutableArray<ScenarioResult> Scenarios => Run.Scenarios;
    public ImmutableArray<AssuranceArtifact> Artifacts => Report.Artifacts.IsDefault ? [] : Report.Artifacts;
    public bool Succeeded => Failures.IsEmpty;
}
