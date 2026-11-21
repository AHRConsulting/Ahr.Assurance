using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

/// <summary>How a verification's execution ended.</summary>
public enum VerificationExecutionState
{
    /// <summary>Execution ended without an error. Steps may have failed or not run.</summary>
    Completed,
    /// <summary>Execution stopped on an error, such as an invalid scenario definition. The evidence may be partial.</summary>
    Failed,
    /// <summary>The verification was canceled. The evidence may be partial.</summary>
    Canceled,
    /// <summary>Execution did not finish, for a reason other than an error or a cancellation. The evidence may be partial.</summary>
    Incomplete
}

/// <summary>Portable attachment metadata. Content is resolved separately; never serialized as live objects.</summary>
public sealed record EvidenceAttachment(
    string Id, string Name, string MediaType, string Reference,
    string? ScenarioId = null, int? StepIndex = null, bool Available = true, bool DetailsWithheld = false);

internal interface IAttachmentContentSource
{
    Task<Stream> OpenReadAsync(EvidenceAttachment attachment, CancellationToken cancellationToken = default);
}

/// <summary>One captured verification, independent of its delivery destinations and test framework.</summary>
public sealed record VerificationEvidence(
    string VerificationId, string CorrelationId, string Producer, string? TestId, string? TestName,
    VerificationExecutionState ExecutionState, AssuranceReport Report,
    ImmutableArray<EvidenceAttachment> Attachments)
{
    /// <summary>How the correlation ID was chosen: <c>options</c>, <c>environment</c> or <c>process</c>.</summary>
    public string CorrelationSource { get; init; } = "unspecified";

    /// <summary>The folder attachment references are relative to, itself relative to the evidence file. Null means the evidence file's folder.</summary>
    public string? AttachmentsDirectory { get; init; }

    /// <summary>The Ahr.Assurance version that recorded this evidence, such as <c>0.1.0</c>. Null when it wasn't recorded.</summary>
    public string? AssuranceVersion { get; init; }

    internal void Validate() => Check().OrThrow(static error => new InvalidDataException(error.Message));

    internal Result Check() =>
        Checks.All(this,
            static evidence => Checks.Text(evidence.VerificationId, "verificationId"),
            static evidence => Checks.Text(evidence.CorrelationId, "correlationId"),
            static evidence => Checks.Text(evidence.CorrelationSource, "correlationSource"),
            static evidence => Checks.Text(evidence.Producer, "producer"),
            static evidence => Checks.Present(evidence.Report, "report"),
            static evidence => Checks.That(Enum.IsDefined(evidence.ExecutionState) && !evidence.Attachments.IsDefault,
                "Invalid execution state or attachment collection."),
            static evidence => evidence.Attachments.IsEmpty ? Result.Success() : evidence.CheckAttachments());

    private Result CheckAttachments()
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        return Attachments.Traverse(attachment => Checks.All((Evidence: this, Attachment: attachment, Ids: ids),
            static item => Checks.Present(item.Attachment, "attachment"),
            static item => Checks.Text(item.Attachment.Id, "id"),
            static item => Checks.Text(item.Attachment.Name, "name"),
            static item => Checks.Text(item.Attachment.MediaType, "mediaType"),
            static item => Checks.Text(item.Attachment.Reference, "reference"),
            static item => item.Ids.Add(item.Attachment.Id) ? Result.Success() : $"Duplicate attachment ID '{item.Attachment.Id}'.".ToFailure(),
            static item => item.Evidence.CheckPlacement(item.Attachment)));
    }

    private Result CheckPlacement(EvidenceAttachment attachment) =>
        attachment.ScenarioId.ToOption().Match(
            scenarioId => Report.Run.Scenarios.SingleOrNone(item => item.Id == scenarioId)
                .ToResult(new Error($"Unknown attachment scenario '{scenarioId}'."))
                .Bind(scenario => Checks.That(attachment.StepIndex is not int index || (index >= 0 && index < scenario.Steps.Length),
                    "Attachment step index is outside its scenario.")),
            () => Checks.That(attachment.StepIndex is null, "A step attachment must identify its scenario."));
}

internal sealed record EvidenceDocument(
    int SchemaVersion, string VerificationId, string CorrelationId, string CorrelationSource, string Producer,
    string? TestId, string? TestName, VerificationExecutionState ExecutionState,
    ReportSnapshot Evidence, ImmutableArray<EvidenceAttachment> Attachments, string? AttachmentsDirectory = null,
    string? AssuranceVersion = null)
{
    public const int CurrentSchemaVersion = 1;

    public static EvidenceDocument Capture(VerificationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        var snapshot = ReportSnapshot.Capture(evidence.Report);
        snapshot.Check().OrThrow(static error => new InvalidDataException(error.Message));
        return new(CurrentSchemaVersion, evidence.VerificationId, evidence.CorrelationId, evidence.CorrelationSource, evidence.Producer, evidence.TestId,
            evidence.TestName, evidence.ExecutionState, snapshot, evidence.Attachments, evidence.AttachmentsDirectory, evidence.AssuranceVersion);
    }

    public Result<VerificationEvidence> ToEvidence() =>
        Checks.That(SchemaVersion == CurrentSchemaVersion, $"Unsupported evidence schema version '{SchemaVersion}'.")
            .Bind(() => Checks.Present(Evidence, "evidence"))
            .Bind(() => Evidence.ToReport())
            .Bind(report => Result.Try(() => new VerificationEvidence(VerificationId, CorrelationId, Producer, TestId, TestName, ExecutionState, report, Attachments)
            {
                CorrelationSource = CorrelationSource,
                AttachmentsDirectory = AttachmentsDirectory,
                AssuranceVersion = AssuranceVersion
            }, EvidenceDocumentJson.IsInvalidValue))
            .Bind(evidence => evidence.Check().Bind(() => evidence.ToSuccess()));
}

internal sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(allowIntegerValues: false)
    where TEnum : struct, Enum;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    Converters =
    [
        typeof(StrictEnumConverter<VerificationExecutionState>),
        typeof(StrictEnumConverter<StepKind>),
        typeof(StrictEnumConverter<StepKeyword>),
        typeof(StrictEnumConverter<AssuranceStatus>)
    ])]
[JsonSerializable(typeof(EvidenceDocument))]
internal sealed partial class EvidenceJsonContext : JsonSerializerContext;

internal static class EvidenceDocumentJson
{
    private static readonly EvidenceJsonContext _compact = new(new JsonSerializerOptions(EvidenceJsonContext.Default.Options)
    {
        WriteIndented = false
    });

    internal static JsonSerializerOptions Options => EvidenceJsonContext.Default.Options;

    public static string Serialize(VerificationEvidence evidence, bool writeIndented = true) =>
        JsonSerializer.Serialize(EvidenceDocument.Capture(evidence), TypeInfo(writeIndented));

    public static byte[] SerializeToUtf8Bytes(VerificationEvidence evidence, bool writeIndented = true) =>
        JsonSerializer.SerializeToUtf8Bytes(EvidenceDocument.Capture(evidence), TypeInfo(writeIndented));

    public static Result<VerificationEvidence> Read([StringSyntax(StringSyntaxAttribute.Json)] string json) =>
        Result.Try(() => SchemaVersion(json), IsUnreadable)
            .Bind(version => version.ToResult(new Error("An integer schemaVersion is required.")))
            .Bind(version => Checks.That(version == EvidenceDocument.CurrentSchemaVersion, $"Unsupported evidence schema version '{version}'."))
            .Bind(() => Result.Try(() => JsonSerializer.Deserialize(json, EvidenceJsonContext.Default.EvidenceDocument).ToOption(), IsUnreadable))
            .Bind(document => document.ToResult(new Error("An evidence document is required.")))
            .Bind(document => document.ToEvidence());

    private static Option<int> SchemaVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Option<JsonElement>.Some(document.RootElement)
            .Where(root => root.ValueKind == JsonValueKind.Object)
            .Bind(root => Property(root, "schemaVersion"))
            .Bind(WholeNumber);
    }

    private static Option<JsonElement> Property(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? Option<JsonElement>.Some(value) : Option<JsonElement>.None;

    private static Option<int> WholeNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? Option<int>.Some(number) : Option<int>.None;

    internal static bool IsInvalidValue(Exception exception) => exception is ArgumentException or InvalidOperationException;

    private static bool IsUnreadable(Exception exception) => exception is JsonException or ArgumentException or NotSupportedException;

    private static JsonTypeInfo<EvidenceDocument> TypeInfo(bool writeIndented) =>
        writeIndented ? EvidenceJsonContext.Default.EvidenceDocument : _compact.EvidenceDocument;
}
