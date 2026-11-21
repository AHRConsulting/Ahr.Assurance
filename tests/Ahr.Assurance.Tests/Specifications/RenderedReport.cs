using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests.Specifications;

internal sealed partial class RenderedReport
{
    private readonly Dictionary<string, BusinessContext> _areas = new(StringComparer.Ordinal);

    public List<ScenarioResult> Journeys { get; } = [];

    public List<ReportAttachmentView> Attachments { get; } = [];

    public List<ReportProjectSection> Projects { get; } = [];

    public DeveloperReportSection? Developer { get; set; }

    public string Title { get; set; } = "Release";

    public string? Scope { get; set; }

    public DateTimeOffset? Generated { get; set; }

    public int MaxTextLength { get; set; } = 4096;

    public string Html { get; private set; } = "";

    public string Text { get; private set; } = "";

    public AssuranceReport Report => new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero, [.. Journeys]), []);

    public BusinessContext Area(string name)
    {
        if (!_areas.TryGetValue(name, out BusinessContext? area))
        {
            var id = name.ToLowerInvariant();
            area = BusinessContext.Define(id, name, name + " purpose").Capability(id + "-c", "Capability").Outcome(id + "-o", name + " outcome").Build();
            _areas.Add(name, area);
        }

        return area;
    }

    public void Add(BusinessContext? business, AssuranceStatus status, string expected = "RAW-EXPECTED")
    {
        var number = (Journeys.Count + 1).ToString(CultureInfo.InvariantCulture);
        Journeys.Add(new("journey-id-" + number, "Journey " + number, business,
            [new("Checked result", StepKind.Then, new(status, "RAW-EXPLANATION", expected, "RAW-ACTUAL"), TimeSpan.FromSeconds(1))]));
    }

    public void AddRow(string name, string currency, AssuranceStatus status, BusinessContext? business = null) =>
        Journeys.Add(new($"{name} [currency: {currency}]", name, business, [new("It is recorded", StepKind.Then, new(status, "Checked"), TimeSpan.Zero)])
        {
            OutlineId = name,
            Example = [new("currency", currency)]
        });

    public void Render()
    {
        Html = new StakeholderReportWriter(MaxTextLength).Render(Report, Title, [.. Attachments], Developer, Scope,
            Projects.Count == 0 ? default : [.. Projects], Generated);
        Text = Visible(Html[Html.IndexOf("<body", StringComparison.Ordinal)..]);
    }

    public string Summary(string name)
    {
        var start = SummaryStart(name);
        return Visible(Html[start..Html.IndexOf("</summary>", start, StringComparison.Ordinal)]);
    }

    public bool IsOpen(string name)
    {
        var summary = SummaryStart(name);
        var details = Html.LastIndexOf("<details", summary, StringComparison.Ordinal);
        return Html[details..summary].EndsWith(" open>", StringComparison.Ordinal);
    }

    public int PositionOf(string text)
    {
        var position = Html.IndexOf(text, StringComparison.Ordinal);
        Assert.True(position >= 0, $"Expected the report to show '{text}'.");
        return position;
    }

    private int SummaryStart(string name)
    {
        var start = Html.IndexOf("<summary>" + WebUtility.HtmlEncode(name) + " ", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected the report to show '{name}' as a journey or an area.");
        return start;
    }

    private static string Visible(string html) =>
        Whitespace().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

internal sealed class RenderedReportSteps : ScenarioSteps<RenderedReport>
{
    public static ScenarioAction<RenderedReport> AJourney(AssuranceStatus status) =>
        CreateActionStep($"A journey outside any business area that is {status}", report => report.Add(null, status));

    public static ScenarioAction<RenderedReport> AJourneyIn(string area, AssuranceStatus status) =>
        CreateActionStep($"A journey in {area} that is {status}", report => report.Add(report.Area(area), status));

    public static ScenarioAction<RenderedReport> TheReportIsRendered { get; } =
        CreateActionStep("The report is rendered", report => report.Render());

    public static ScenarioAction<RenderedReport> TheJourneysAreGrouped { get; } =
        CreateActionStep("The journeys are grouped by business context", report => _ = report.Report.BusinessGroups);

    public static ScenarioAction<RenderedReport> TheGroupingIsRejected { get; } =
        CreateThrowsStep<InvalidOperationException>("The journeys are grouped by business context", report => _ = report.Report.BusinessGroups);

    public static ScenarioExpectation<RenderedReport> TheReportSays(string text) =>
        CreateAssertionStep($"The report says '{text}'", report => Assert.Contains(text, report.Text));

    public static ScenarioExpectation<RenderedReport> TheReportDoesNotSay(string text) =>
        CreateAssertionStep($"The report doesn't say '{text}'", report =>
            Assert.DoesNotContain(text, report.Text, StringComparison.OrdinalIgnoreCase));

    public static ScenarioExpectation<RenderedReport> IsOpen(string name) =>
        CreateAssertionStep($"'{name}' is open", report => Assert.True(report.IsOpen(name), $"Expected '{name}' to be open."));

    public static ScenarioExpectation<RenderedReport> IsClosed(string name) =>
        CreateAssertionStep($"'{name}' is closed", report => Assert.False(report.IsOpen(name), $"Expected '{name}' to be closed."));

    public static ScenarioExpectation<RenderedReport> ShowsAttachments(string name, int count) =>
        CreateAssertionStep($"'{name}' shows {count} attachments", report =>
        {
            var summary = report.Summary(name);
            if (count == 0)
            {
                Assert.DoesNotContain("attachment", summary, StringComparison.Ordinal);
            }
            else
            {
                Assert.EndsWith(count == 1 ? "1 attachment" : $"{count} attachments", summary);
            }
        });

    public static ScenarioExpectation<RenderedReport> TheJourneysFormGroups(int count) =>
        CreateAssertionStep($"The journeys form {count} business groups", report => Assert.Equal(count, report.Report.BusinessGroups.Length));
}
