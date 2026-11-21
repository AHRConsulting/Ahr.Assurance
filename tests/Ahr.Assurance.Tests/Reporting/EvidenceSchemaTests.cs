using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests.Reporting;

public sealed class EvidenceSchemaTests
{
    private static string SchemaPath => PublicTree.PathOf("schemas", $"assurance-evidence.v{EvidenceDocument.CurrentSchemaVersion}.json");

    [Fact]
    public async Task PublishedSchemaMatchesTheEvidenceFormat()
    {
        JsonNode generated = JsonSchemaExporter.GetJsonSchemaAsNode(EvidenceDocumentJson.Options, typeof(EvidenceDocument),
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        var expected = generated.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        if (Environment.GetEnvironmentVariable("ASSURANCE_UPDATE_SCHEMA") == "1")
        {
            await File.WriteAllTextAsync(SchemaPath, expected, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(SchemaPath), $"Missing {SchemaPath}. Run with ASSURANCE_UPDATE_SCHEMA=1 to create it.");
        var published = await File.ReadAllTextAsync(SchemaPath, TestContext.Current.CancellationToken);
        Assert.True(published == expected,
            "The evidence format changed. Bump the schema version if needed, then run with ASSURANCE_UPDATE_SCHEMA=1.");
    }

    [Fact]
    public void ReferencesAndCorrelationSourceSurviveARoundTrip()
    {
        BusinessContext business = BusinessContext.Define("access", "Access", "Keep access")
            .Capability("c", "Capability").Outcome("o", "Outcome")
            .Reference("jira", "DOC-42", new Uri("https://example.test/DOC-42"))
            .Reference("ado", "7")
            .Build();
        VerificationEvidence evidence = new("id", "run", "producer", null, null, VerificationExecutionState.Completed,
            new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
            [
                new("scenario", "Scenario", business, [new("Check", StepKind.Then, new(AssuranceStatus.Satisfied, "Done"), TimeSpan.Zero)])
                {
                    References = [new("jira", "DOC-43")]
                }
            ]), [], []), [])
        {
            CorrelationSource = "environment"
        };

        VerificationEvidence restored = EvidenceDocumentJson.Read(EvidenceDocumentJson.Serialize(evidence)).Value;
        ScenarioResult scenario = restored.Report.Run.Scenarios[0];

        Assert.Equal("environment", restored.CorrelationSource);
        Assert.Equal(new ExternalReference("jira", "DOC-43"), Assert.Single(scenario.References));
        Assert.Equal(2, scenario.Business!.References.Length);
        Assert.Equal(new Uri("https://example.test/DOC-42"), scenario.Business.References[0].Url);
        Assert.Null(scenario.Business.References[1].Url);
    }

    [Theory]
    [InlineData("\"verdict\": \"Satisfied\"", "\"verdict\": 0")]
    [InlineData("\"correlationSource\": \"options\",", "")]
    [InlineData("\"references\": [],", "")]
    public void ImportRejectsIntegerEnumsAndMissingRequiredFields(string find, string replacement)
    {
        VerificationEvidence evidence = new("id", "run", "producer", null, null, VerificationExecutionState.Completed,
            new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
            [
                new("scenario", "Scenario", null, [new("Check", StepKind.Then, new(AssuranceStatus.Satisfied, "Done"), TimeSpan.Zero)])
            ]), [], []), [])
        {
            CorrelationSource = "options"
        };
        var json = EvidenceDocumentJson.Serialize(evidence);
        Assert.Contains(find, json);

        Assert.True(EvidenceDocumentJson.Read(json.Replace(find, replacement, StringComparison.Ordinal)).IsFailure);
    }

    [Fact]
    public void ImportRejectsAReferenceUrlThatIsNotHttp()
    {
        BusinessContext business = BusinessContext.Define("Keep access").Capability("c", "Capability").Outcome("o", "Outcome")
            .Reference("jira", "DOC-42", new Uri("https://example.test/DOC-42")).Build();
        VerificationEvidence evidence = new("id", "run", "producer", null, null, VerificationExecutionState.Completed,
            new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
                [new("scenario", "Scenario", business, [new("Check", StepKind.Then, new(AssuranceStatus.Satisfied, "Done"), TimeSpan.Zero)])]), [], []), []);
        var json = EvidenceDocumentJson.Serialize(evidence).Replace("https://example.test/DOC-42", "javascript:alert(1)", StringComparison.Ordinal);

        Assert.True(EvidenceDocumentJson.Read(json).IsFailure);
    }
}
