using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting.Html;

internal sealed class HtmlReportFormat : IReportFormat
{
    public string Name => "html";

    public async Task<ReportFormatResult> WriteAsync(ReportFormatContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ReportFormatContext options = context;
        var outputDirectory = context.OutputDirectory;
        Item[] items = [.. context.Inputs.SelectMany((input, section) => input.Results.Verifications.Select(evidence => new Item(section, input.Results, evidence)))];
        List<ReportDiagnostic> failures = [];
        HashSet<string> placed = new(StringComparer.Ordinal);
        (ImmutableArray<ReportAttachmentView> attachments, Dictionary<string, ImmutableArray<EvidenceAttachment>> copied) =
            await AttachmentsAsync(items, outputDirectory, options, failures, placed, cancellationToken).ConfigureAwait(false);
        DeveloperReportSection? developer = options.Developer
            ? new(await DeveloperAsync(items, outputDirectory, options, copied, failures, cancellationToken).ConfigureAwait(false))
            : null;

        Item[] shown = [.. items.Where(item => IsShown(item, options))];
        DateTimeOffset start = shown.Min(item => item.Evidence.Report.Run.StartedAt);
        DateTimeOffset end = shown.Max(item => item.Evidence.Report.Run.StartedAt + item.Evidence.Report.Run.Duration);
        AssuranceReport combined = new(new(StakeholderReportWriter.DefaultTitle, start, end - start,
            [.. shown.SelectMany(item => item.Evidence.Report.Run.Scenarios.Where(options.Shows))]),
            [.. shown.SelectMany(item => item.Evidence.Report.Diagnostics)]);
        ImmutableArray<ReportProjectSection> sections = Sections(shown, context);
        ImmutableArray<BusinessReportGroup> groups = Groups(combined.Run, sections);
        var scope = options.Contexts.IsDefaultOrEmpty ? null
            : string.Join(", ", groups.Select(group => group.Business.Name ?? group.Business.Description));
        var single = groups.Length == 1 && groups[0].Scenarios.Length == combined.Run.Scenarios.Length ? groups[0].Business.Name : null;
        var title = options.Title ?? (options.Contexts.IsDefaultOrEmpty ? null : single) ?? options.DefaultTitle ?? single ?? StakeholderReportWriter.DefaultTitle;
        var html = new StakeholderReportWriter().Render(EvidenceExport.Report(combined, options.IncludeDiagnosticDetails), title, attachments, developer, scope,
            sections, DateTimeOffset.UtcNow, Tests(context)?.Describe(shown.Length), Excluded(items, options), Tests(context)?.Failed ?? 0);
        var path = Path.Combine(outputDirectory, "report.html");
        await ReportArtifactWriter.WriteAsync(async (stream, ct) =>
            await stream.WriteAsync(Encoding.UTF8.GetBytes(html), ct).ConfigureAwait(false), path, cancellationToken).ConfigureAwait(false);
        _ = Result.Try(() => RemoveEarlierFiles(items, outputDirectory, placed, options.Developer ? shown.Length : 0), IsFileProblem)
            .TapError(error => failures.Add(CleanupError(error)));

        return new()
        {
            Path = path,
            Diagnostics = [.. failures]
        };
    }

    private sealed record Item(int Section, AssuranceResults Folder, VerificationEvidence Evidence);

    private static string? Excluded(Item[] items, ReportFormatContext options)
    {
        if (options.ExcludedContexts.IsDefaultOrEmpty || !options.Contexts.IsDefaultOrEmpty)
        {
            return null;
        }
        string[] names = [.. items.SelectMany(item => item.Evidence.Report.Run.Scenarios)
            .Select(scenario => scenario.Business).OfType<BusinessContext>()
            .Where(business => business.Id is { } id && options.ExcludedContexts.Contains(id, StringComparer.Ordinal))
            .Select(business => business.Name ?? business.Description)
            .Distinct(StringComparer.Ordinal)];
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static bool IsShown(Item item, ReportFormatContext options) =>
        item.Evidence.Report.Run.Scenarios.IsEmpty || item.Evidence.Report.Run.Scenarios.Any(options.Shows);

    private static void RemoveEarlierFiles(Item[] items, string outputDirectory, HashSet<string> placed, int pages)
    {
        var attachments = Path.GetFullPath(Path.Combine(outputDirectory, "attachments"));
        if (Directory.Exists(attachments) && !items.Any(item => IsWithin(Path.GetFullPath(item.Folder.GetAttachmentsDirectory(item.Evidence)), attachments)))
        {
            foreach (var file in Directory.EnumerateFiles(attachments).Where(file => !placed.Contains(Path.GetFullPath(file))).ToArray())
            {
                File.Delete(file);
            }
        }

        var verifications = Path.Combine(outputDirectory, "verifications");
        if (Directory.Exists(verifications))
        {
            foreach (var folder in Directory.EnumerateDirectories(verifications).Where(folder => IsPage(Path.GetFileName(folder), pages)).ToArray())
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static void RemoveOtherFiles(string folder, ImmutableArray<AssuranceArtifact> artifacts, List<ReportDiagnostic> failures)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        HashSet<string> written = [.. artifacts.Select(artifact => Path.GetFullPath(artifact.Location))];
        _ = Result.Try(() => Array.ForEach([.. Directory.EnumerateFiles(folder).Where(file => !written.Contains(Path.GetFullPath(file)))], File.Delete),
                IsFileProblem)
            .TapError(error => failures.Add(CleanupError(error)));
    }

    private static bool IsFileProblem(Exception exception) => exception is IOException or UnauthorizedAccessException;

    private static ReportDiagnostic CleanupError(Error error) =>
        new("report-cleanup-error", $"Files from an earlier report could not be removed: {error.Message}", error.Exception?.GetType().FullName);

    private static bool IsPage(string name, int pages) =>
        name.Length == 6 && name.All(char.IsAsciiDigit) && int.Parse(name, CultureInfo.InvariantCulture) >= pages;

    private static bool IsWithin(string path, string folder) =>
        string.Equals(path, folder, StringComparison.Ordinal) || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static ImmutableArray<BusinessReportGroup> Groups(AssuranceRunResult run, ImmutableArray<ReportProjectSection> sections)
    {
        if (sections.IsEmpty)
        {
            return BusinessReportGrouping.Create(run);
        }
        return [.. sections.SelectMany(section => BusinessReportGrouping.Create(run with { Scenarios = [.. run.Scenarios.Skip(section.Start).Take(section.Count)] }))];
    }

    private static TestRunSummary? Tests(ReportFormatContext context) =>
        context.Inputs.All(input => input.Tests is not null) ? TestRunSummary.Combine(context.Inputs.Select(input => input.Tests!)) : null;

    private static ImmutableArray<ReportProjectSection> Sections(Item[] shown, ReportFormatContext context)
    {
        if (!context.Inputs.Any(input => input.Project is not null))
        {
            return [];
        }

        ImmutableArray<ReportProjectSection>.Builder sections = ImmutableArray.CreateBuilder<ReportProjectSection>();
        var start = 0;
        for (var section = 0; section < context.Inputs.Length; section++)
        {
            Item[] inputs = [.. shown.Where(item => item.Section == section)];
            var count = inputs.Sum(item => item.Evidence.Report.Run.Scenarios.Count(context.Shows));
            if (count != 0)
            {
                ReportFormatInput input = context.Inputs[section];
                sections.Add(new(input.Project ?? Path.GetFileName(input.Results.Directory), inputs.Min(item => item.Evidence.Report.Run.StartedAt), start, count));
                start += count;
            }
        }

        return sections.ToImmutable();
    }

    private static async Task<(ImmutableArray<ReportAttachmentView> Views, Dictionary<string, ImmutableArray<EvidenceAttachment>> Copied)> AttachmentsAsync(
        Item[] items, string outputDirectory, ReportFormatContext options, List<ReportDiagnostic> failures, HashSet<string> written, CancellationToken token)
    {
        ImmutableArray<ReportAttachmentView>.Builder views = ImmutableArray.CreateBuilder<ReportAttachmentView>();
        Dictionary<string, ImmutableArray<EvidenceAttachment>> copied = new(StringComparer.Ordinal);
        var offset = 0;
        foreach (Item item in items)
        {
            VerificationEvidence evidence = item.Evidence;
            ScenarioResult[] scenarios = [.. evidence.Report.Run.Scenarios.Where(options.Shows)];
            var folder = item.Folder.GetAttachmentsDirectory(evidence);
            AttachmentFolderSource source = new(folder);
            ImmutableArray<EvidenceAttachment>.Builder placed = ImmutableArray.CreateBuilder<EvidenceAttachment>();
            foreach (EvidenceAttachment attachment in evidence.Attachments)
            {
                EvidenceAttachment unavailable = attachment with
                {
                    Reference = AttachmentCapture.MissingReference,
                    Available = false
                };
                if (!IsShown(attachment, scenarios, evidence.Report.Run.Scenarios.Length))
                {
                    placed.Add(unavailable);
                    continue;
                }

                int? scenario = attachment.ScenarioId is not null
                    ? offset + Array.FindIndex(scenarios, value => value.Id == attachment.ScenarioId)
                    : null;
                if (!attachment.Available)
                {
                    placed.Add(attachment);
                    views.Add(new(scenario, attachment.StepIndex, attachment.Name, Problem: "was not available when this run was recorded."));
                    continue;
                }

                var relative = $"attachments/{EvidenceFiles.SafeId(evidence.VerificationId)}-{EvidenceFiles.SafeId(attachment.Id)}{AttachmentBundle.Extension(attachment)}";
                var destination = Path.Combine(outputDirectory, relative);
                Result delivered = await Result.TryAsync(
                    () => CopyAttachmentAsync(source, attachment, Path.GetFullPath(attachment.Reference, folder), destination, token),
                    static _ => true).ConfigureAwait(false);
                Placement placement = delivered.Match(
                    () => Included(scenario, attachment, relative, destination),
                    error => Excluded(scenario, attachment, unavailable, error));
                placed.Add(placement.Attachment);
                views.Add(placement.View);
                _ = placement.Written.Tap(path => written.Add(path));
                _ = placement.Failure.Tap(failures.Add);
            }

            copied[evidence.VerificationId] = placed.ToImmutable();
            offset += scenarios.Length;
        }

        return (views.ToImmutable(), copied);
    }

    private static bool IsShown(EvidenceAttachment attachment, ScenarioResult[] shownScenarios, int totalScenarios) =>
        attachment.ScenarioId is { } id ? shownScenarios.Any(value => value.Id == id) : shownScenarios.Length == totalScenarios;

    private static async Task<string> DeveloperAsync(Item[] items, string outputDirectory, ReportFormatContext options,
        Dictionary<string, ImmutableArray<EvidenceAttachment>> copied, List<ReportDiagnostic> failures, CancellationToken token)
    {
        StringBuilder listing = new();
        Item[] shown = [.. items.Where(item => IsShown(item, options))];
        TestRunSummary? tests = Tests(options);
        _ = listing.Append("<p>").Append(WebUtility.HtmlEncode(tests?.Describe(shown.Length)
                ?? string.Create(CultureInfo.InvariantCulture, $"{shown.Length} recorded verifications. Suite completion not established."))).Append("</p>");
        if (tests is { NotPassed.IsEmpty: false })
        {
            _ = listing.Append("<p>Tests that did not pass:</p><ul>");
            foreach (var name in tests.NotPassed)
            {
                _ = listing.Append("<li>").Append(WebUtility.HtmlEncode(name)).Append("</li>");
            }

            _ = listing.Append("</ul>");
        }

        _ = listing
            .Append("<p>Selected correlation: ")
            .Append(WebUtility.HtmlEncode(string.Join(", ", shown.Select(item => item.Evidence.CorrelationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))))
            .Append(" (source: ")
            .Append(WebUtility.HtmlEncode(string.Join(", ", shown.Select(item => item.Evidence.CorrelationSource).Distinct(StringComparer.Ordinal))))
            .Append(")</p><h3>Verifications</h3><ul>");
        var ordinal = 0;
        foreach (Item item in shown)
        {
            var relative = $"verifications/{ordinal++:D6}";
            var destination = Path.Combine(outputDirectory, relative);
            EvidenceDeliveryResult delivered = await EvidencePublisher.PublishAsync(item.Evidence with
            {
                Attachments = copied[item.Evidence.VerificationId]
            },
                [ReportSinks.Json(), ReportSinks.Html()],
                new(destination)
                {
                    AttachmentsDirectory = Path.Combine(outputDirectory, "attachments"),
                    IncludeDiagnosticDetails = options.IncludeDiagnosticDetails
                }, token).ConfigureAwait(false);
            failures.AddRange(delivered.Diagnostics);
            RemoveOtherFiles(destination, delivered.Artifacts, failures);
            AssuranceArtifact? html = delivered.Artifacts.SingleOrDefault(artifact => artifact.Name == "HTML");
            _ = listing.Append("<li>");
            _ = html is not null
                ? listing.Append("<a href=\"").Append(WebUtility.HtmlEncode(relative + "/" + Path.GetFileName(html.Location)))
                    .Append("\">").Append(WebUtility.HtmlEncode(item.Evidence.Report.Run.Name)).Append("</a>")
                : listing.Append(WebUtility.HtmlEncode(item.Evidence.Report.Run.Name)).Append(" (HTML unavailable)");
            _ = listing.Append(" - ").Append(item.Evidence.Report.Run.Status).Append("; execution ").Append(item.Evidence.ExecutionState)
                .Append("; ").Append(WebUtility.HtmlEncode(item.Evidence.TestName ?? item.Evidence.VerificationId)).Append("</li>");
        }

        _ = listing.Append("</ul>");
        foreach (ReportDiagnostic failure in failures)
        {
            ReportDiagnostic visible = EvidenceExport.Diagnostic(failure, options.IncludeDiagnosticDetails);
            _ = listing.Append("<p>Delivery error [").Append(WebUtility.HtmlEncode(visible.Code)).Append("]: ")
                .Append(WebUtility.HtmlEncode(visible.Message)).Append("</p>");
        }

        return listing.ToString();
    }

    private static async Task CopyAttachmentAsync(AttachmentFolderSource source, EvidenceAttachment attachment, string current, string destination,
        CancellationToken cancellationToken)
    {
        if (string.Equals(current, destination, StringComparison.Ordinal))
        {
            return;
        }

        await using Stream content = await source.OpenReadAsync(attachment, cancellationToken).ConfigureAwait(false);
        await ReportArtifactWriter.WriteAsync(content.CopyToAsync, destination, cancellationToken).ConfigureAwait(false);
    }

    private sealed record Placement(EvidenceAttachment Attachment, ReportAttachmentView View, Option<string> Written, Option<ReportDiagnostic> Failure);

    private static Placement Included(int? scenario, EvidenceAttachment attachment, string relative, string destination) =>
        new(attachment with
        {
            Reference = Path.GetFileName(relative)
        },
        new(scenario, attachment.StepIndex, attachment.Name, Link: relative, Image: StakeholderReportWriter.IsImage(attachment.MediaType)),
        Option<string>.Some(Path.GetFullPath(destination)),
        Option<ReportDiagnostic>.None);

    private static Placement Excluded(int? scenario, EvidenceAttachment attachment, EvidenceAttachment unavailable, Error error)
    {
        if (error.Exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(unavailable, new(scenario, attachment.StepIndex, attachment.Name, Problem: "is not available."), Option<string>.None, Option<ReportDiagnostic>.None);
        }

        return new(unavailable, new(scenario, attachment.StepIndex, attachment.Name, Problem: "could not be included in this report."), Option<string>.None,
            Option<ReportDiagnostic>.Some(new("report-attachment-delivery-error", $"Attachment '{attachment.Name}': {error.Message}", error.Exception?.GetType().FullName)));
    }
}
