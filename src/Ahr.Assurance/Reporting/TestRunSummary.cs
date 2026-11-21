using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal sealed record TestRunSummary(string CorrelationId, int Passed, int Failed, int Skipped, ImmutableArray<string> NotPassed)
{
    internal const string Folder = "summary";
    private const int MaximumNames = 200;

    internal int Total => Passed + Failed + Skipped;

    internal string Describe(int verifications) => string.Create(CultureInfo.InvariantCulture,
        $"{Total} {(Total == 1 ? "test" : "tests")} ran: {Passed} passed, {Failed} failed, {Skipped} skipped. {verifications} recorded scenario evidence.");

    internal static TestRunSummary Combine(IEnumerable<TestRunSummary> summaries)
    {
        TestRunSummary[] all = [.. summaries];
        return new(string.Join(", ", all.Select(summary => summary.CorrelationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
            all.Sum(summary => summary.Passed), all.Sum(summary => summary.Failed), all.Sum(summary => summary.Skipped),
            [.. all.SelectMany(summary => summary.NotPassed).Distinct(StringComparer.Ordinal).Take(MaximumNames)]);
    }

    internal async Task WriteAsync(string runFolder, CancellationToken cancellationToken)
    {
        var folder = Directory.CreateDirectory(Path.Combine(runFolder, Folder)).FullName;
        TestRunSummary trimmed = this with { NotPassed = [.. NotPassed.Take(MaximumNames)] };
        await File.WriteAllBytesAsync(Path.Combine(folder, $"tests-{Guid.NewGuid():N}.json"),
            JsonSerializer.SerializeToUtf8Bytes(trimmed, TestRunSummaryJsonContext.Default.TestRunSummary), cancellationToken).ConfigureAwait(false);
    }

    internal static Option<TestRunSummary> Read(string runFolder, IEnumerable<string> correlations)
    {
        HashSet<string> runs = new(correlations, StringComparer.Ordinal);
        return Option<string>.Some(Path.Combine(runFolder, Folder)).Where(Directory.Exists)
            .Bind(folder => ReadFolder(folder)
                .Map(summaries => summaries.Where(summary => runs.Contains(summary.CorrelationId)).ToArray())
                .Ensure(summaries => summaries.Length != 0 && summaries.All(IsWellFormed), "The run folder has no readable summary for these runs.")
                .Map(Combine)
                .ToOption());
    }

    private static Result<IReadOnlyList<TestRunSummary>> ReadFolder(string folder) =>
        Result.Try(() => Directory.GetFiles(folder, "tests-*.json"), IsUnreadable)
            .Bind(files => files.Order(StringComparer.Ordinal).Traverse(ReadFile));

    private static Result<TestRunSummary> ReadFile(string file) =>
        Result.Try(() => JsonSerializer.Deserialize(File.ReadAllBytes(file), TestRunSummaryJsonContext.Default.TestRunSummary).ToOption(), IsUnreadable)
            .Bind(summary => summary.ToResult(new Error("An empty test-run summary.")));

    private static bool IsWellFormed(TestRunSummary summary) =>
        !summary.NotPassed.IsDefault && summary.Passed >= 0 && summary.Failed >= 0 && summary.Skipped >= 0;

    private static bool IsUnreadable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(TestRunSummary))]
internal sealed partial class TestRunSummaryJsonContext : JsonSerializerContext;
