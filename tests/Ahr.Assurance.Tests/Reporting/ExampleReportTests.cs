using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests;

public sealed class ExampleReportTests
{
    [Fact]
    public void ExamplesSurviveTheEvidenceDocument()
    {
        VerificationEvidence evidence = Evidence([Row("A charge is recorded", "GBP", AssuranceStatus.Satisfied)]);

        ScenarioResult restored = EvidenceDocumentJson.Read(EvidenceDocumentJson.Serialize(evidence)).Value.Report.Run.Scenarios.Single();

        Assert.Equal("A charge is recorded", restored.OutlineId);
        Assert.Equal([new StepParameter("currency", "GBP")], restored.Example);
    }

    private static ScenarioResult Row(string name, string currency, AssuranceStatus status) =>
        new($"{name} [currency: {currency}]", name, null, [Step(status)])
        {
            OutlineId = name,
            Example = [new("currency", currency)]
        };

    private static StepResult Step(AssuranceStatus status) => new("It is recorded", StepKind.Then, new(status, "Checked"), TimeSpan.Zero);

    private static VerificationEvidence Evidence(ImmutableArray<ScenarioResult> scenarios) =>
        new("v-1", "run", "producer", "test", "Test", VerificationExecutionState.Completed,
            new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, scenarios), []), []);
}
