using System.Collections.Immutable;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting;

/// <summary>The outcome of a verification: its run and the files its evidence sinks wrote.</summary>
/// <param name="Run">The result of running the verification's scenarios.</param>
/// <param name="Artifacts">The files the evidence sinks wrote.</param>
public sealed record AssuranceVerificationResult(AssuranceRunResult Run, ImmutableArray<AssuranceArtifact> Artifacts)
{
    /// <summary>The verification's name.</summary>
    public string Name => Run.Name;

    /// <summary>The verification's verdict.</summary>
    public AssuranceStatus Status => Run.Status;

    /// <summary>The results of the verification's scenarios.</summary>
    public ImmutableArray<ScenarioResult> Scenarios => Run.Scenarios;
}
