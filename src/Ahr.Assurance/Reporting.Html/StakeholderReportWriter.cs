using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Reporting.Html;

internal sealed record ReportAttachmentView(int? ScenarioIndex, int? StepIndex, string Name, string? Link = null, bool Image = false, string? Problem = null);

internal sealed record DeveloperReportSection(string Html);

internal sealed record ReportProjectSection(string Title, DateTimeOffset Tested, int Start, int Count);

internal sealed class StakeholderReportWriter : IAssuranceReporter
{
    internal const string DefaultTitle = "Delivery report";
    private static readonly Lazy<string> _logo = new(ReadLogo);
    private readonly int _maxTextLength;

    public StakeholderReportWriter(int maxTextLength = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTextLength);
        _maxTextLength = maxTextLength;
    }

    private const string Styles = """
        :root {
          color-scheme: light; font-family: system-ui, -apple-system, "Segoe UI", sans-serif; line-height: 1.55;
          --ink: #172033; --muted: #55607a; --line: #dfe3eb; --page: #f5f6f9; --card: #ffffff; --link: #144d9e;
          --pass: #16603a; --pass-soft: #e8f5ee; --fail: #a32020; --fail-soft: #fcecec; --warn: #7a5200; --warn-soft: #fdf3d8; --none: #55607a; --none-soft: #eef0f5;
          color: var(--ink); background: var(--page);
        }
        body { margin: 0; }
        .page { max-width: 64rem; margin: auto; padding: 2rem 1.5rem; }
        .kind { font-size: .75rem; font-weight: 600; letter-spacing: .08em; text-transform: uppercase; color: var(--muted); margin: 0 0 .25rem; }
        h1 { font-size: 1.9rem; line-height: 1.2; margin: 0 0 .25rem; letter-spacing: -.01em; }
        h2 { font-size: 1.15rem; margin: 0 0 .75rem; } h3 { font-size: .8rem; letter-spacing: .08em; text-transform: uppercase; color: var(--muted); margin: 1.5rem 0 .5rem; }
        a { color: var(--link); } a:focus-visible, summary:focus-visible { outline: 3px solid var(--link); outline-offset: 2px; }
        .meta, .note { color: var(--muted); } .note { font-size: .9rem; margin: .25rem 0 0; }
        .banner { border-left: .35rem solid currentColor; border-radius: .35rem; padding: 1rem 1.25rem; margin: 1.5rem 0 1rem; }
        .banner .note, .banner p { color: var(--ink); } .banner .headline { font-size: 1.3rem; font-weight: 700; margin: 0; }
        .satisfied { color: var(--pass); background: var(--pass-soft); }
        .unsatisfied { color: var(--fail); background: var(--fail-soft); }
        .inconclusive { color: var(--warn); background: var(--warn-soft); }
        .notevaluated { color: var(--none); background: var(--none-soft); }
        .error { color: var(--fail); background: var(--fail-soft); border-radius: .35rem; padding: .5rem .75rem; }
        .status { display: inline-block; white-space: nowrap; font-size: .8rem; font-weight: 600; border-radius: 1rem; padding: .05rem .6rem; vertical-align: .05em; }
        .tiles { display: flex; flex-wrap: wrap; gap: .75rem; margin: 0 0 1.5rem; }
        .tile { flex: 1 1 9rem; background: var(--card); border: 1px solid var(--line); border-radius: .35rem; padding: .75rem 1rem; }
        .tile span { display: block; font-size: .75rem; letter-spacing: .08em; text-transform: uppercase; color: var(--muted); }
        .tile strong { font-size: 1.35rem; font-variant-numeric: tabular-nums; }
        .roles { display: flex; flex-wrap: wrap; gap: .75rem; font-size: .95rem; font-variant-numeric: tabular-nums; margin: -.75rem 0 .5rem; }
        .role { flex: 1 1 11rem; background: var(--card); font-weight: 600; border: 1px solid var(--line); border-left: .25rem solid currentColor; border-radius: .25rem; padding: .5rem .75rem; }
        .role strong { color: var(--ink); font-weight: 700; }
        .explain { font-size: .8rem; margin: 0 0 1.5rem; }
        section { background: var(--card); border: 1px solid var(--line); border-radius: .35rem; padding: 1.25rem; margin: 1rem 0; }
        table { width: 100%; border-collapse: collapse; margin: .25rem 0 1rem; font-size: .95rem; } caption { text-align: left; font-weight: 600; padding: .25rem 0; }
        th { font-size: .75rem; letter-spacing: .06em; text-transform: uppercase; color: var(--muted); background: var(--page); font-weight: 600; }
        th, td { text-align: left; vertical-align: top; padding: .5rem .6rem; border-bottom: 1px solid var(--line); overflow-wrap: anywhere; }
        td { font-variant-numeric: tabular-nums; } th:nth-child(2), td:nth-child(2) { width: 8rem; } th:nth-child(3), td:nth-child(3) { width: 34%; }
        td ul { margin: .25rem 0 0; }
        summary { cursor: pointer; font-weight: 600; overflow-wrap: anywhere; }
        details.area { border-top: 1px solid var(--line); padding: .85rem 0; } details.area:first-of-type { border-top: 0; padding-top: 0; }
        details.area > summary { font-size: 1.05rem; } .detail { padding: .5rem 0 0 1rem; }
        .counts { display: inline-block; font-size: .9rem; font-weight: 400; color: var(--muted); margin-left: .25rem; }
        .attached { display: inline-flex; align-items: center; gap: .2rem; white-space: nowrap; font-size: .85rem; font-weight: 400; color: var(--muted); margin-left: .35rem; }
        .attached svg { width: .9rem; height: .9rem; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
        .project { border-top: 2px solid var(--line); margin-top: 2.5rem; padding-top: 1rem; } .project > h2 { font-size: 1.5rem; margin: 0; }
        details.journey { border-top: 1px solid var(--line); padding: .6rem 0; } details.journey details.journey { margin-left: 1rem; }
        table.examples th, table.examples td { width: auto; }
        ol, ul { padding-left: 1.25rem; } li { margin: .3rem 0; overflow-wrap: anywhere; }
        li.problem { background: var(--fail-soft); border-radius: .25rem; padding: .25rem .5rem; }
        figure { margin: .5rem 0; } figcaption { color: var(--muted); font-size: .85rem; }
        img { display: block; max-width: min(100%, 36rem); height: auto; border: 1px solid var(--line); border-radius: .25rem; }
        pre { white-space: pre-wrap; overflow-wrap: anywhere; background: var(--page); border-radius: .25rem; padding: .75rem; font-size: .85rem; }
        footer { display: flex; align-items: center; gap: .5rem; color: var(--muted); font-size: .8rem; border-top: 1px solid var(--line); margin-top: 2rem; padding-top: .75rem; }
        footer svg { width: 2rem; height: 2rem; flex: none; }
        @media (max-width: 40rem) {
          .page { padding: 1.25rem 1rem; } h1 { font-size: 1.5rem; } section { padding: .9rem; } .detail { padding-left: 0; }
          .tiles { gap: .5rem; } .tile { flex-basis: 5.5rem; padding: .5rem .6rem; } .tile span { font-size: .65rem; letter-spacing: .04em; } .tile strong { display: block; font-size: 1.05rem; } .roles { gap: .5rem; margin-top: -1rem; }
          table, thead, tbody, tr, th, td, caption { display: block; width: auto !important; } thead { display: none; }
          tr { border-top: 1px solid var(--line); padding: .5rem 0; } td { border: 0; padding: .1rem 0; }
          td[data-label]::before { content: attr(data-label) ": "; color: var(--muted); }
        }
        """;

    private static string BannerCss(AssuranceStatus? verdict, int otherFailures) =>
        verdict is null ? "notevaluated" : Css(otherFailures > 0 && verdict == AssuranceStatus.Satisfied ? AssuranceStatus.Unsatisfied : verdict.Value);

    public string Render(AssuranceReport report, string title, ImmutableArray<ReportAttachmentView> attachments = default, DeveloperReportSection? developer = null, string? scope = null,
        ImmutableArray<ReportProjectSection> projects = default, DateTimeOffset? generated = null, string? testRun = null, string? excluded = null,
        int failedTests = 0)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        AssuranceRunResult run = report.Run;
        ReportProjectSection[] sections = projects.IsDefault ? []
            : [.. projects.OrderBy(project => Severity(Verdict.Aggregate(run.Scenarios.Skip(project.Start).Take(project.Count).Select(scenario => scenario.Status))))];
        ImmutableArray<ReportAttachmentView> placed = attachments.IsDefault ? [] : attachments;
        var attachedTo = placed.Where(item => item.ScenarioIndex is not null)
            .GroupBy(item => item.ScenarioIndex!.Value).ToDictionary(group => group.Key, group => group.Count());
        Dictionary<ScenarioResult, int> journeyIndex = new(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < run.Scenarios.Length; index++)
        {
            journeyIndex.Add(run.Scenarios[index], index);
        }

        StringBuilder html = new(16 * 1024);
        _ = html.Append("""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src 'self' file:">
            """).Append("<meta name=\"generator\" content=\"AHR Assurance ").Append(Text(AssuranceVersion.Current)).Append("\">").Append("<title>").Append(Text(DocumentTitle(title))).Append("</title><style>").Append(Styles)
            .Append("</style></head><body><div class=\"page\"><header>").Append(KindLabel(title)).Append("<h1>").Append(Text(title)).Append("</h1><p class=\"meta\">");
        _ = sections.Length == 0 ? html.Append("Tested ").Append(Time(run.StartedAt)) : html.Append(sections.Length).Append(sections.Length == 1 ? " project" : " projects");
        _ = generated is { } at ? html.Append(". Report generated ").Append(Time(at)) : html;
        _ = html.Append("</p></header><main>");

        BusinessReportGroup[] groups = [];
        List<Area> areas = [];
        List<DeclaredConcern> concerns = [];
        ScenarioResult[] scenarios = [];
        var summarise = false;
        var passed = 0;
        var total = 0;
        if (sections.Length == 0)
        {
            Body([.. run.Scenarios], "", true);
        }
        else
        {
            Overview();
            for (var index = 0; index < sections.Length; index++)
            {
                ReportProjectSection section = sections[index];
                var id = "project-" + index.ToString(CultureInfo.InvariantCulture);
                _ = html.Append("<div class=\"project\" id=\"").Append(id).Append("\"><h2>").Append(Text(section.Title)).Append("</h2><p class=\"meta\">Tested ")
                    .Append(Time(section.Tested)).Append("</p>");
                Body([.. run.Scenarios.Skip(section.Start).Take(section.Count)], id + "-", false);
                _ = html.Append("</div>");
            }
        }

        ReportAttachmentView[] unplaced = [.. placed.Where(item => item.ScenarioIndex is null)];
        if (unplaced.Length != 0)
        {
            _ = html.Append("<section id=\"attachments\"><h2>Attachments</h2>");
            foreach (ReportAttachmentView attachment in unplaced)
            {
                Attachment(attachment);
            }

            _ = html.Append("</section>");
        }

        _ = html.Append("</main>");
        if (developer is not null)
        {
            _ = html.Append("<section id=\"developer\"><h2>Developer details</h2>").Append(developer.Html).Append("</section>");
        }

        return html.Append("<footer>").Append(_logo.Value).Append("<span>AHR Assurance</span></footer></div></body></html>").ToString();

        void Body(ScenarioResult[] shown, string prefix, bool top)
        {
            scenarios = shown;
            groups = [.. new AssuranceReport(run with { Scenarios = [.. shown] }, []).BusinessGroups];
            HashSet<ScenarioResult> grouped = new(groups.SelectMany(group => group.Scenarios), ReferenceEqualityComparer.Instance);
            ScenarioResult[] other = [.. scenarios.Where(scenario => !grouped.Contains(scenario))];
            areas = [];
            for (var index = 0; index < groups.Length; index++)
            {
                BusinessReportGroup group = groups[index];
                areas.Add(new(prefix + "business-group-" + index.ToString(CultureInfo.InvariantCulture), group.Business.Name ?? group.Business.Description, [.. group.Scenarios], group));
            }

            if (other.Length != 0)
            {
                areas.Add(new(prefix + "other-checks", groups.Length == 0 ? "Journeys" : "Other checks", other, null));
            }

            summarise = areas.Count > 1;
            concerns = [.. groups.SelectMany(Concerns)];
            ScenarioResult[][] counted = Journeys(scenarios);
            passed = counted.Count(Passed);
            total = counted.Length;
            AssuranceStatus verdict = Verdict.Aggregate(scenarios.Select(scenario => scenario.Status));
            var otherFailures = top ? failedTests : 0;
            _ = html.Append("<div id=\"").Append(prefix).Append("summary\" class=\"banner ")
                .Append(BannerCss(scenarios.Length == 0 ? null : verdict, otherFailures)).Append("\"><p class=\"headline\">");
            Headline();
            OtherFailures(verdict, otherFailures);
            _ = html.Append("</p>");
            if (top)
            {
                Note();
            }

            _ = html.Append("</div>");
            if (top)
            {
                Incomplete();
            }

            _ = html.Append("<div class=\"tiles\">");
            if (summarise)
            {
                Tile("Areas", areas.Count(area => area.Status == AssuranceStatus.Satisfied), areas.Count);
            }

            Tile("Journeys", passed, total);
            _ = html.Append("</div>");
            if (concerns.Count != 0)
            {
                _ = html.Append("<div class=\"roles\">");
                Role("Risks", DeclaredConcern.Rows(groups, business => business.Risks));
                Role("Rules", DeclaredConcern.Rows(groups, business => business.Rules));
                Role("Outcomes", DeclaredConcern.Rows(groups, business => business.Outcomes));
                _ = html.Append("</div><p class=\"note explain\">A journey is one tested scenario. ").Append(Capitalise(RoleNames()))
                    .Append(" are passing when all linked journeys pass.</p>");
            }

            if (summarise)
            {
                _ = html.Append("<section id=\"").Append(prefix).Append("areas\" aria-label=\"Areas\"><h2>Areas</h2>");
            }

            foreach (Area area in areas.OrderBy(area => Severity(area.Status)).ThenBy(area => area.Group is null).ThenBy(area => area.Name, StringComparer.Ordinal))
            {
                AreaStart(area);
                if (area.Group is { } group)
                {
                    BusinessContext business = group.Business;
                    if (business.Name is not null)
                    {
                        _ = html.Append("<p>").Append(Text(business.Description)).Append("</p>");
                    }

                    References(business.References);
                    AtStake(group);
                    _ = html.Append("<h3>Journeys</h3>");
                }
                else if (groups.Length != 0)
                {
                    _ = html.Append("<p>These are not linked to a declared risk, rule or outcome.</p>");
                }

                foreach (ScenarioResult[] journeys in Outlines(area.Scenarios))
                {
                    if (journeys.Length == 1)
                    {
                        Journey(journeys[0]);
                    }
                    else
                    {
                        Outline(journeys);
                    }
                }

                AreaEnd();
            }

            if (summarise)
            {
                _ = html.Append("</section>");
            }
        }

        void Overview()
        {
            AssuranceStatus[] statuses = [.. sections.Select(section =>
                Verdict.Aggregate(run.Scenarios.Skip(section.Start).Take(section.Count).Select(scenario => scenario.Status)))];
            var problems = statuses.Count(status => status != AssuranceStatus.Satisfied);
            AssuranceStatus verdict = Verdict.Aggregate(statuses);
            _ = html.Append("<div id=\"summary\" class=\"banner ").Append(BannerCss(verdict, failedTests)).Append("\"><p class=\"headline\">");
            _ = problems == 0
                ? html.Append("All ").Append(sections.Length).Append(sections.Length == 1 ? " project passed its checks." : " projects passed their checks.")
                : html.Append(problems).Append(" of ").Append(sections.Length).Append(problems == 1 ? " projects needs attention." : " projects need attention.");
            OtherFailures(verdict, failedTests);
            _ = html.Append("</p>");
            Note();
            _ = html.Append("</div>");
            Incomplete();
            _ = html.Append("<section id=\"projects\" aria-label=\"Projects\"><h2>Projects</h2><table><thead><tr><th scope=\"col\">Project</th>")
                .Append("<th scope=\"col\">Result</th><th scope=\"col\">Tested</th></tr></thead><tbody>");
            for (var index = 0; index < sections.Length; index++)
            {
                _ = html.Append("<tr><td><a href=\"#project-").Append(index).Append("\">").Append(Text(sections[index].Title)).Append("</a></td><td>");
                Badge(statuses[index]);
                _ = html.Append("</td><td data-label=\"Tested\">").Append(Time(sections[index].Tested)).Append("</td></tr>");
            }

            _ = html.Append("</tbody></table></section>");
        }

        void OtherFailures(AssuranceStatus verdict, int count)
        {
            if (count > 0 && verdict == AssuranceStatus.Satisfied)
            {
                _ = html.Append(count == 1 ? " But 1 other test failed." : " But " + count.ToString(CultureInfo.InvariantCulture) + " other tests failed.");
            }
        }

        void Note()
        {
            _ = html.Append("<p class=\"note\">This covers only ");
            if (scope is not null)
            {
                _ = html.Append(Text(scope)).Append(", and only the checks included in this run. Other areas are not shown");
            }
            else if (excluded is not null)
            {
                _ = html.Append("the checks included in this run, except ").Append(Text(excluded)).Append(", which this report leaves out");
            }
            else
            {
                _ = html.Append("the checks included in this run");
            }
            _ = html.Append(". A pass shows that those checks passed, not that everything has been tested.</p>");
            if (testRun is not null)
            {
                _ = html.Append("<p class=\"note\">").Append(Text(testRun)).Append("</p>");
            }
        }

        void Incomplete()
        {
            if (!report.Diagnostics.IsEmpty)
            {
                _ = html.Append("<p class=\"error\">Some checks could not finish properly, so these results may be incomplete. Ask the engineering team to review this run.</p>");
            }
        }

        void Tile(string label, int value, int total) =>
            _ = html.Append("<div class=\"tile\"><span>").Append(label).Append("</span><strong>").Append(value).Append(" of ").Append(total).Append("</strong> passed</div>");

        void Role(string label, List<DeclaredConcern> rows)
        {
            if (rows.Count != 0)
            {
                AssuranceStatus status = Verdict.Aggregate(rows.Select(row => row.Status));
                _ = html.Append("<span class=\"role ").Append(Css(status)).Append("\">").Append(Icon(status)).Append("<strong>").Append(label).Append("</strong> ")
                    .Append(rows.Count(row => row.Status == AssuranceStatus.Satisfied)).Append(" of ").Append(rows.Count).Append(" passing</span>");
            }
        }

        static string ConcernsHeadline(int problems, int count, string roles)
        {
            if (count == 1)
            {
                var role = roles.TrimEnd('s');
                return problems == 0 ? $"The {role} is passing." : $"The {role} is not passing.";
            }
            if (problems == 0)
            {
                return $"All {count} {roles} are passing.";
            }
            return problems == 1 ? $"1 of {count} {roles} is not passing." : $"{problems} of {count} {roles} are not passing.";
        }

        string RoleNames()
        {
            List<string> names = [];
            if (groups.Any(group => !group.Business.Risks.IsEmpty))
            {
                names.Add("risks");
            }

            if (groups.Any(group => !group.Business.Rules.IsEmpty))
            {
                names.Add("rules");
            }

            if (groups.Any(group => !group.Business.Outcomes.IsEmpty))
            {
                names.Add("outcomes");
            }

            return names.Count == 1 ? names[0] : string.Join(", ", names.SkipLast(1)) + " and " + names[^1];
        }

        void AreaStart(Area area)
        {
            if (!summarise)
            {
                _ = html.Append("<section id=\"").Append(area.Id).Append("\"><h2>").Append(Text(area.Name)).Append("</h2>");
                return;
            }

            _ = html.Append("<details class=\"area\" id=\"").Append(area.Id).Append('"')
                .Append(area.Status == AssuranceStatus.Satisfied ? "" : " open").Append("><summary>").Append(Text(area.Name)).Append(' ');
            Badge(area.Status);
            ScenarioResult[][] journeys = Journeys(area.Scenarios);
            _ = html.Append(" <span class=\"counts\">").Append(JourneysPassed(journeys.Count(Passed), journeys.Length)).Append("</span>");
            Attached(area.Scenarios.Sum(scenario => attachedTo.GetValueOrDefault(journeyIndex[scenario])));
            _ = html.Append("</summary><div class=\"detail\">");
        }

        void AreaEnd() => _ = html.Append(summarise ? "</div></details>" : "</section>");

        void Headline()
        {
            if (summarise)
            {
                var problems = areas.Count(area => area.Status != AssuranceStatus.Satisfied);
                _ = problems == 0
                    ? html.Append("All ").Append(areas.Count).Append(" areas passed their checks.")
                    : html.Append(problems).Append(" of ").Append(areas.Count).Append(problems == 1 ? " areas needs attention." : " areas need attention.");
            }
            else if (concerns.Count != 0)
            {
                var problems = concerns.Count(concern => concern.Status != AssuranceStatus.Satisfied);
                _ = html.Append(ConcernsHeadline(problems, concerns.Count, RoleNames()));
            }
            else
            {
                _ = html.Append(JourneysPassed(passed, total)).Append('.');
            }
        }

        static List<DeclaredConcern> Concerns(BusinessReportGroup group) =>
        [
            .. DeclaredConcern.Rows([group], business => business.Risks),
            .. DeclaredConcern.Rows([group], business => business.Rules),
            .. DeclaredConcern.Rows([group], business => business.Outcomes)
        ];

        void AtStake(BusinessReportGroup group)
        {
            _ = html.Append("<div class=\"at-stake\"><h3>What is at stake</h3>");
            ConcernTable("Risks", DeclaredConcern.Rows([group], business => business.Risks));
            ConcernTable("Rules", DeclaredConcern.Rows([group], business => business.Rules));
            ConcernTable("Outcomes", DeclaredConcern.Rows([group], business => business.Outcomes));
            _ = html.Append("</div>");
        }

        void ConcernTable(string label, List<DeclaredConcern> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            _ = html.Append("<table><caption>").Append(label).Append("</caption><thead><tr>");
            _ = html.Append("<th scope=\"col\">").Append(label[..^1]).Append("</th><th scope=\"col\">Result</th><th scope=\"col\">Journeys</th></tr></thead><tbody>");
            foreach (DeclaredConcern row in rows.OrderBy(row => Severity(row.Status)))
            {
                _ = html.Append("<tr>");
                _ = html.Append("<td>").Append(Text(row.Item.Name)).Append("</td><td>");
                Badge(row.Status);
                ScenarioResult[][] journeys = Journeys(row.Journeys);
                _ = html.Append("</td><td data-label=\"Journeys\">").Append(journeys.Count(Passed)).Append(" of ").Append(journeys.Length).Append(" passed");
                ScenarioResult[] problems = [.. journeys.Where(journey => !Passed(journey)).Select(FirstProblem)];
                if (problems.Length != 0)
                {
                    _ = html.Append("<ul>");
                    foreach (ScenarioResult problem in problems)
                    {
                        _ = html.Append("<li><a href=\"#journey-").Append(journeyIndex[problem]).Append("\">")
                            .Append(Text(problem.Name)).Append("</a></li>");
                    }

                    _ = html.Append("</ul>");
                }

                _ = html.Append("</td></tr>");
            }

            _ = html.Append("</tbody></table>");
        }

        void Outline(ScenarioResult[] rows)
        {
            AssuranceStatus status = Verdict.Aggregate(rows.Select(row => row.Status));
            _ = html.Append("<details class=\"journey outline\"").Append(status == AssuranceStatus.Satisfied ? "" : " open").Append("><summary>")
                .Append(Text(rows[0].Name)).Append(' ');
            Badge(status);
            _ = html.Append(" <span class=\"counts\">").Append(rows.Count(row => row.Status == AssuranceStatus.Satisfied))
                .Append(" of ").Append(rows.Length).Append(" examples passed</span>");
            Attached(rows.Sum(row => attachedTo.GetValueOrDefault(journeyIndex[row])));
            _ = html.Append("</summary>");
            if (rows[0].Description is { } description)
            {
                _ = html.Append("<p>").Append(Text(description)).Append("</p>");
            }

            string[] columns = [.. rows.SelectMany(row => row.Example.Select(value => value.Name)).Distinct(StringComparer.Ordinal)];
            _ = html.Append("<table class=\"examples\"><caption>Examples</caption><thead><tr>");
            foreach (var column in columns)
            {
                _ = html.Append("<th scope=\"col\">").Append(Text(column)).Append("</th>");
            }

            _ = html.Append("<th scope=\"col\">Result</th></tr></thead><tbody>");
            foreach (ScenarioResult row in rows)
            {
                _ = html.Append("<tr>");
                foreach (var column in columns)
                {
                    _ = html.Append("<td data-label=\"").Append(Text(column)).Append("\">")
                        .Append(Text(row.Example.FirstOrDefault(value => value.Name == column)?.Value ?? "")).Append("</td>");
                }

                _ = html.Append("<td data-label=\"Result\"><a href=\"#journey-").Append(journeyIndex[row]).Append("\">");
                Badge(row.Status);
                _ = html.Append("</a></td></tr>");
            }

            _ = html.Append("</tbody></table>");
            foreach (ScenarioResult row in rows)
            {
                Journey(row, "Example: " + string.Join(", ", row.Example.Select(value => $"{value.Name} = {value.Value}")));
            }

            _ = html.Append("</details>");
        }

        void Journey(ScenarioResult scenario, string? title = null)
        {
            var index = journeyIndex[scenario];
            _ = html.Append("<details class=\"journey\" id=\"journey-").Append(index).Append('"')
                .Append(scenario.Status == AssuranceStatus.Satisfied ? "" : " open").Append("><summary>")
                .Append(Text(title ?? scenario.Name)).Append(' ');
            Badge(scenario.Status);
            Attached(attachedTo.GetValueOrDefault(index));
            _ = html.Append("</summary>");
            if (title is null && scenario.Description is { } description)
            {
                _ = html.Append("<p>").Append(Text(description)).Append("</p>");
            }

            References(scenario.References);
            _ = html.Append("<ol>");
            for (var stepIndex = 0; stepIndex < scenario.Steps.Length; stepIndex++)
            {
                StepResult step = scenario.Steps[stepIndex];
                _ = html.Append(step.Evaluation.Status == AssuranceStatus.Unsatisfied ? "<li class=\"problem\">" : "<li>").Append(step.Keyword).Append(' ').Append(Text(step.Name)).Append(' ');
                if (ShowsVerdict(step))
                {
                    Badge(step.Evaluation.Status);
                }

                if (developer is not null && step.Description is { } stepDescription)
                {
                    _ = html.Append("<p class=\"note\">").Append(Text(stepDescription)).Append("</p>");
                }

                if (developer is not null)
                {
                    TracedValues(step.Parameters);
                }

                if (developer is not null && step.Evaluation.Status != AssuranceStatus.Satisfied)
                {
                    _ = html.Append("<pre>").Append(Text(step.Evaluation.Explanation));
                    _ = step.Evaluation.Expected is { } expected ? html.Append("\nExpected: ").Append(Text(expected)) : html;
                    _ = step.Evaluation.Actual is { } actual ? html.Append("\nActual: ").Append(Text(actual)) : html;
                    _ = html.Append("</pre>");
                }

                foreach (ReportAttachmentView attachment in placed.Where(item => item.ScenarioIndex == index && item.StepIndex == stepIndex))
                {
                    Attachment(attachment);
                }

                _ = html.Append("</li>");
            }

            _ = html.Append("</ol>");
            foreach (ReportAttachmentView attachment in placed.Where(item => item.ScenarioIndex == index && item.StepIndex is null))
            {
                Attachment(attachment);
            }

            _ = html.Append("</details>");
        }

        void TracedValues(ImmutableArray<StepParameter> values)
        {
            if (values.IsDefaultOrEmpty)
            {
                return;
            }

            _ = html.Append("<pre class=\"values\">");
            for (var index = 0; index < values.Length; index++)
            {
                _ = html.Append(index == 0 ? "" : "\n").Append(Text(values[index].Name)).Append(" = ").Append(Text(values[index].Value));
            }

            _ = html.Append("</pre>");
        }

        void Attached(int count)
        {
            if (count != 0)
            {
                _ = html.Append("<span class=\"attached\"><svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"m21.4 11.1-9.2 9.2a6 6 0 0 1-8.5-8.5l9.2-9.2a4 4 0 0 1 5.7 5.7l-9.2 9.2a2 2 0 0 1-2.8-2.8l8.5-8.5\"/></svg>")
                    .Append(count).Append(count == 1 ? " attachment" : " attachments").Append("</span>");
            }
        }

        void Attachment(ReportAttachmentView attachment)
        {
            var name = Text(attachment.Name);
            _ = attachment switch
            {
                { Problem: { } problem } => html.Append("<p class=\"error\">").Append(name).Append(": ").Append(Text(problem)).Append("</p>"),
                { Link: { } link, Image: true } => html.Append("<figure><a href=\"").Append(WebUtility.HtmlEncode(link)).Append("\"><img src=\"").Append(WebUtility.HtmlEncode(link))
                    .Append("\" alt=\"").Append(name).Append("\" loading=\"lazy\"></a><figcaption>").Append(name).Append("</figcaption></figure>"),
                { Link: { } link } => html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(link)).Append("\">").Append(name).Append("</a></p>"),
                _ => html
            };
        }

        void References(ImmutableArray<ExternalReference> references)
        {
            if (references.IsDefaultOrEmpty)
            {
                return;
            }

            _ = html.Append("<p class=\"references\">Tracked in: ");
            for (var index = 0; index < references.Length; index++)
            {
                ExternalReference reference = references[index];
                _ = html.Append(index == 0 ? "" : ", ");
                var label = Text(reference.Provider) + " " + Text(reference.Key);
                _ = reference.Url is { } url
                    ? html.Append("<a href=\"").Append(WebUtility.HtmlEncode(url.AbsoluteUri)).Append("\">").Append(label).Append("</a>")
                    : html.Append(label);
            }

            _ = html.Append("</p>");
        }

        void Badge(AssuranceStatus status) =>
            _ = html.Append("<span class=\"status ").Append(Css(status)).Append("\">")
                .Append(Icon(status)).Append(StatusLabel(status)).Append("</span>");
    }

    private static IEnumerable<ScenarioResult[]> Outlines(IEnumerable<ScenarioResult> scenarios)
    {
        List<List<ScenarioResult>> groups = [];
        Dictionary<string, List<ScenarioResult>> outlines = new(StringComparer.Ordinal);
        foreach (ScenarioResult scenario in scenarios)
        {
            if (scenario.OutlineId is { } outline && outlines.TryGetValue(outline, out List<ScenarioResult>? rows))
            {
                rows.Add(scenario);
                continue;
            }

            List<ScenarioResult> group = [scenario];
            groups.Add(group);
            if (scenario.OutlineId is { } id)
            {
                outlines[id] = group;
            }
        }

        return groups.Select(group => group.ToArray());
    }

    private static ScenarioResult[][] Journeys(IEnumerable<ScenarioResult> scenarios) => [.. Outlines(scenarios)];

    private static string JourneysPassed(int passed, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"{passed} of {total} {(total == 1 ? "journey" : "journeys")} passed");

    private static bool Passed(ScenarioResult[] journey) => journey.All(row => row.Status == AssuranceStatus.Satisfied);

    private static ScenarioResult FirstProblem(ScenarioResult[] journey) => journey.First(row => row.Status != AssuranceStatus.Satisfied);

    private static string Time(DateTimeOffset value) => value.ToUniversalTime().ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public async Task WriteAsync(AssuranceReport report, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        var html = Render(report, string.IsNullOrWhiteSpace(report.Run.Name) ? DefaultTitle : report.Run.Name, developer: new(Diagnostics(report.Diagnostics)));
        await destination.WriteAsync(new UTF8Encoding(false).GetBytes(html), cancellationToken).ConfigureAwait(false);
    }

    internal string Diagnostics(IEnumerable<ReportDiagnostic> diagnostics)
    {
        StringBuilder html = new();
        foreach (ReportDiagnostic diagnostic in diagnostics)
        {
            _ = html.Append("<p>").Append(diagnostic.Code.StartsWith("report-", StringComparison.Ordinal) ? "Delivery error" : "Execution error")
                .Append(" [").Append(Text(diagnostic.Code)).Append("]: ").Append(Text(diagnostic.Message));
            _ = diagnostic.ExceptionType is { } type ? html.Append(" (").Append(Text(type)).Append(')') : html;
            _ = html.Append("</p>");
        }

        return html.ToString();
    }

    internal static bool IsImage(string mediaType) => mediaType is "image/png" or "image/jpeg" or "image/gif" or "image/webp";

    internal static bool ShowsVerdict(StepResult step) =>
        step.Kind == StepKind.Then || step.Evaluation.Status != AssuranceStatus.Satisfied;

    internal static string StatusLabel(AssuranceStatus status) => status switch
    {
        AssuranceStatus.Satisfied => "Passed",
        AssuranceStatus.Unsatisfied => "Failed",
        AssuranceStatus.Inconclusive => "Inconclusive",
        AssuranceStatus.NotEvaluated => "Not evaluated",
        _ => throw new InvalidOperationException("Unknown assurance status.")
    };

    internal static int Severity(AssuranceStatus status) => status switch
    {
        AssuranceStatus.Unsatisfied => 0,
        AssuranceStatus.NotEvaluated => 1,
        AssuranceStatus.Inconclusive => 2,
        AssuranceStatus.Satisfied => 3,
        _ => 4
    };

    private static string Icon(AssuranceStatus status) => status switch
    {
        AssuranceStatus.Satisfied => "\u2713 ",
        AssuranceStatus.Unsatisfied => "\u2715 ",
        AssuranceStatus.Inconclusive => "! ",
        AssuranceStatus.NotEvaluated => "! ",
        _ => throw new InvalidOperationException("Unknown assurance status.")
    };

    private static bool IsDefaultTitle(string title) => string.Equals(title, DefaultTitle, StringComparison.Ordinal);

    private static string DocumentTitle(string title) => IsDefaultTitle(title) ? title : title + " \u2013 " + DefaultTitle;

    private static string KindLabel(string title) => IsDefaultTitle(title) ? "" : "<p class=\"kind\">" + DefaultTitle + "</p>";

    private static string ReadLogo()
    {
        using Stream stream = typeof(StakeholderReportWriter).Assembly.GetManifestResourceStream("Ahr.Assurance.Logo.svg")
            ?? throw new InvalidOperationException("The report logo is missing from the assembly.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Trim();
    }

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Css(AssuranceStatus status) => status.ToString().ToLowerInvariant();

    private sealed record Area(string Id, string Name, ScenarioResult[] Scenarios, BusinessReportGroup? Group)
    {
        internal AssuranceStatus Status { get; } = Verdict.Aggregate(Scenarios.Select(scenario => scenario.Status));
    }

    private string Text(string text)
    {
        var bounded = text.Length > _maxTextLength ? text[.._maxTextLength] + " [truncated]" : text;
        return WebUtility.HtmlEncode(bounded);
    }
}

internal sealed record DeclaredConcern(string? Context, BusinessItem Item, List<ScenarioResult> Journeys, AssuranceStatus Status)
{
    internal static List<DeclaredConcern> Rows<TItem>(IEnumerable<BusinessReportGroup> groups, Func<BusinessContext, ImmutableArray<TItem>> select)
        where TItem : BusinessItem
    {
        List<DeclaredConcern> rows = [];
        foreach (BusinessReportGroup group in groups)
        {
            Dictionary<string, List<ScenarioResult>> linked = new(StringComparer.Ordinal);
            foreach (ScenarioResult scenario in group.Scenarios)
            {
                foreach (TItem item in select(scenario.Business!))
                {
                    if (!linked.TryGetValue(item.Id, out List<ScenarioResult>? journeys))
                    {
                        linked.Add(item.Id, journeys = []);
                    }
                    journeys.Add(scenario);
                }
            }

            foreach (TItem item in select(group.Business))
            {
                List<ScenarioResult> journeys = linked[item.Id];
                rows.Add(new(group.Business.Name ?? group.Business.Description, item, journeys,
                    Verdict.Aggregate(journeys.Select(journey => journey.Status))));
            }
        }

        return rows;
    }
}
