using System.Collections.Immutable;
using System.Text.Json;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal sealed partial record AssuranceConfig(string Path, string? Runs, string? Attachments, string? ReportOutput, string? Title, int? Keep)
{

    internal bool ReportAfterRun { get; init; }

    internal string? Version { get; init; }

    internal string? DisplayTitle => Title is null ? null : TitleOr(Title);

    internal string TitleOr(string fallback) => Version is null ? Title ?? fallback : $"{Title ?? fallback} {Version}";

    internal ImmutableArray<string> ExcludedContexts { get; init; } = [];

    internal const string FileName = "ahr-assurance.json";

    internal const string RuntimeKey = "Ahr.Assurance.ConfigFile";

    internal const string DefaultRuns = "artifacts/assurance";
    internal const string DefaultReportOutput = "report";

    internal const string Template = """
        {
          // Each test run gets its own folder in here, named by its run ID. Relative to this file.
          "runs": "artifacts/assurance",
          // Remove older run folders beyond the newest 20 when a run starts.
          // "keep": 20,
          // Inside each run folder.
          "attachments": "attachments",
          "report": {
            // The report's title and the version shown after it. Without a title, the project's or solution's name.
            // "title": "Checkout",
            // "version": "4.2",
            // Inside each run folder.
            "output": "report",
            // Also write the report when 'dotnet test' finishes. 'dotnet assurance report' builds it either way.
            // "afterRun": true,
            // Business context IDs to leave out of the report, such as checks only developers read.
            // "excludeContexts": [ "technical" ]
          }
        }

        """;

    private static readonly JsonDocumentOptions _options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Lazy<Option<AssuranceConfig>> _current = new(() =>
        (AppContext.GetData(RuntimeKey) as string).ToOption().Where(File.Exists).Map(Load));

    internal static Option<AssuranceConfig> Current => _current.Value;

    internal string RunsDirectory => Runs ?? System.IO.Path.GetFullPath(DefaultRuns, System.IO.Path.GetDirectoryName(Path)!);

    internal string Project
    {
        get
        {
            var folder = System.IO.Path.GetDirectoryName(Path)!;
            var project = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.*proj").Order(StringComparer.Ordinal).FirstOrDefault() : null;
            return project is null ? System.IO.Path.GetFileName(folder) : System.IO.Path.GetFileNameWithoutExtension(project);
        }
    }

    internal static Option<string> Find(string directory)
    {
        for (DirectoryInfo? folder = new(System.IO.Path.GetFullPath(directory)); folder is not null; folder = folder.Parent)
        {
            var path = System.IO.Path.Combine(folder.FullName, FileName);
            if (File.Exists(path))
            {
                return Option<string>.Some(path);
            }
        }
        return Option<string>.None;
    }

    internal static AssuranceConfig Load(string path) => Parse(System.IO.Path.GetFullPath(path), File.ReadAllText(path));

    internal static AssuranceConfig Parse(string path, string json) =>
        Read(path, json).OrThrow(error => new InvalidOperationException($"{path} is not valid: {error.Message}", error.Exception));

    internal static Result<AssuranceConfig> Read(string path, string json) =>
        Result.Try(() => JsonDocument.Parse(json, _options), exception => exception is JsonException)
            .Bind(document => FromDocument(path, document));

    private static Result<AssuranceConfig> FromDocument(string path, JsonDocument document)
    {
        using (document)
        {
            return FromRoot(path, document.RootElement);
        }
    }

    private static Result<AssuranceConfig> FromRoot(string path, JsonElement root) =>
        from file in Result<JsonElement>.Success(root).Ensure(IsObject, "The file must hold a JSON object.")
        from report in Optional(file, "report", value => Checked(value, IsObject, "'report' must be an object."))
        from runs in Text(file, "runs", "runs")
        from attachments in Text(file, "attachments", "attachments")
        from output in Within(report, section => Text(section, "output", "report.output"))
        from title in Within(report, section => Text(section, "title", "report.title"))
        from version in Within(report, section => Text(section, "version", "report.version"))
        from keep in Optional(file, "keep", ReadKeep)
        from afterRun in Within(report, section => Optional(section, "afterRun", ReadAfterRun))
        from excluded in Within(report, section => Optional(section, "excludeContexts", ReadExcludedContexts))
        select new AssuranceConfig(path,
            runs.Map(folder => System.IO.Path.GetFullPath(folder, System.IO.Path.GetDirectoryName(path)!)).GetValueOrDefault(),
            attachments.GetValueOrDefault(),
            output.GetValueOrDefault(),
            title.GetValueOrDefault(),
            keep.Match(count => (int?)count, () => null))
        {
            ReportAfterRun = afterRun.GetValueOrDefault(),
            Version = version.GetValueOrDefault(),
            ExcludedContexts = excluded.GetValueOrDefault([])
        };

    private static Result<bool> ReadAfterRun(JsonElement value) =>
        Checked(value, IsBoolean, "'report.afterRun' must be true or false.").Map(flag => flag.GetBoolean());

    private static Result<ImmutableArray<string>> ReadExcludedContexts(JsonElement value) =>
        Checked(value, IsListOfIds, "'report.excludeContexts' must be a list of business context IDs.")
            .Map(contexts => ImmutableArray.CreateRange(contexts.EnumerateArray().Select(context => context.GetString()!).Distinct(StringComparer.Ordinal)));

    private static Result<int> ReadKeep(JsonElement value) =>
        Checked(value, IsPositiveWholeNumber, "'keep' must be a whole number of at least 1.").Map(count => count.GetInt32());

    private static Result<Option<string>> Text(JsonElement section, string name, string label) =>
        Optional(section, name, value => Checked(value, IsNonEmptyText, $"'{label}' must be a non-empty string.").Map(text => text.GetString()!));

    private static Result<Option<T>> Optional<T>(JsonElement section, string name, Func<JsonElement, Result<T>> read) =>
        Property(section, name).Match(value => read(value).Map(Option<T>.Some), () => Result<Option<T>>.Success(Option<T>.None));

    private static Result<Option<T>> Within<T>(Option<JsonElement> section, Func<JsonElement, Result<Option<T>>> read) =>
        section.Match(read, () => Result<Option<T>>.Success(Option<T>.None));

    private static Option<JsonElement> Property(JsonElement section, string name) =>
        section.TryGetProperty(name, out JsonElement value) ? Option<JsonElement>.Some(value).Where(IsPresent) : Option<JsonElement>.None;

    private static Result<JsonElement> Checked(JsonElement value, Func<JsonElement, bool> valid, string message) =>
        Result<JsonElement>.Success(value).Ensure(valid, message);

    private static bool IsPresent(JsonElement value) => value.ValueKind != JsonValueKind.Null;

    private static bool IsObject(JsonElement value) => value.ValueKind == JsonValueKind.Object;

    private static bool IsBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool IsNonEmptyText(JsonElement value) => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool IsPositiveWholeNumber(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count) && count > 0;

    private static bool IsListOfIds(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(IsNonEmptyText);
}
