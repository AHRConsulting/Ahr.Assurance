using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal sealed record BusinessReportGroup(BusinessContext Business, ImmutableArray<ScenarioResult> Scenarios);

internal static class BusinessReportGrouping
{
    internal static ImmutableArray<BusinessReportGroup> Create(AssuranceRunResult run) =>
        Read(run).OrThrow(static error => new InvalidOperationException(error.Message));

    internal static Result<ImmutableArray<BusinessReportGroup>> Read(AssuranceRunResult run) =>
        Scenarios(run).Traverse(Merge).Map(groups => groups.ToImmutableArray());

    private static List<List<ScenarioResult>> Scenarios(AssuranceRunResult run)
    {
        List<List<ScenarioResult>> groups = [];
        Dictionary<string, List<ScenarioResult>> identified = new(StringComparer.Ordinal);
        foreach (ScenarioResult scenario in run.Scenarios)
        {
            if (scenario.Business is not { } business)
            {
                continue;
            }

            if (business.Id is not { } id)
            {
                groups.Add([scenario]);
            }
            else if (identified.TryGetValue(id, out List<ScenarioResult>? existing))
            {
                existing.Add(scenario);
            }
            else
            {
                List<ScenarioResult> group = [scenario];
                identified.Add(id, group);
                groups.Add(group);
            }
        }

        return groups;
    }

    private static Result<BusinessReportGroup> Merge(List<ScenarioResult> scenarios) =>
        from first in scenarios[0].Business.ToOption().ToResult(new Error("A business report group requires metadata."))
        from capabilities in Consistent(first, scenarios).Bind(() => MergeItems(scenarios, static business => business.Capabilities, "capability"))
        from outcomes in MergeItems(scenarios, static business => business.Outcomes, "outcome")
        from rules in MergeItems(scenarios, static business => business.Rules, "rule")
        from risks in MergeItems(scenarios, static business => business.Risks, "risk")
        from objectives in MergeItems(scenarios, static business => business.Objectives, "objective")
        from references in MergeReferences(scenarios)
        from builder in Define(first)
        from business in capabilities.Aggregate(builder, static (current, item) => current.Capability(item))
            .Apply(outcomes, static (current, item) => current.Outcome(item))
            .Apply(rules, static (current, item) => current.Rule(item))
            .Apply(risks, static (current, item) => current.Risk(item))
            .Apply(objectives, static (current, item) => current.Objective(item))
            .Apply(references, static (current, reference) => current.Reference(reference))
            .TryBuild()
        select new BusinessReportGroup(business, [.. scenarios]);

    private static Result Consistent(BusinessContext first, List<ScenarioResult> scenarios) =>
        Checks.Each(scenarios, first, static (scenario, expected) =>
            Checks.That(Agrees(expected, scenario.Business), (First: expected, Other: scenario.Business), static pair => Conflict(pair.First, pair.Other)));

    private static bool Agrees(BusinessContext first, BusinessContext? business) =>
        business is not null && business.Name == first.Name && business.Description == first.Description;

    private static string Conflict(BusinessContext first, BusinessContext? other) =>
        $"Conflicting definition for business context '{first.Id}': '{Label(first)}' and '{(other is null ? "none" : Label(other))}'. Give each context its own ID.";

    private static Result<BusinessContextBuilder> Define(BusinessContext first) =>
        first.Id.ToOption().Match(
            id => first.Name.ToOption().ToResult(new Error("An identified context requires a name."))
                .Map(name => BusinessContext.Define(id, name, first.Description)),
            () => BusinessContext.Define(first.Description).ToSuccess());

    private static BusinessContextBuilder Apply<TItem>(this BusinessContextBuilder builder, IEnumerable<TItem> items,
        Func<BusinessContextBuilder, TItem, BusinessContextBuilder> add) =>
        items.Aggregate(builder, add);

    private static string Label(BusinessContext business) => business.Name ?? business.Description;

    private static Result<IReadOnlyList<ExternalReference>> MergeReferences(List<ScenarioResult> scenarios)
    {
        Dictionary<(string, string), ExternalReference> seen = [];
        List<ExternalReference> merged = [];
        foreach (ScenarioResult scenario in scenarios)
        {
            foreach (ExternalReference reference in scenario.Business!.References)
            {
                if (!seen.TryGetValue((reference.Provider, reference.Key), out ExternalReference? existing))
                {
                    seen.Add((reference.Provider, reference.Key), reference);
                    merged.Add(reference);
                }
                else if (existing.Url != reference.Url)
                {
                    return $"Conflicting URL for reference '{reference.Provider}:{reference.Key}' in business context '{scenario.Business.Id}'."
                        .ToFailure<IReadOnlyList<ExternalReference>>();
                }
            }
        }
        return Result<IReadOnlyList<ExternalReference>>.Success(merged);
    }

    private static Result<IReadOnlyList<TItem>> MergeItems<TItem>(List<ScenarioResult> scenarios, Func<BusinessContext, ImmutableArray<TItem>> select, string role)
        where TItem : BusinessItem
    {
        Dictionary<string, TItem> seen = new(StringComparer.Ordinal);
        List<TItem> merged = [];
        foreach (ScenarioResult scenario in scenarios)
        {
            foreach (TItem item in select(scenario.Business!))
            {
                if (!seen.TryGetValue(item.Id, out TItem? existing))
                {
                    seen.Add(item.Id, item);
                    merged.Add(item);
                }
                else if (existing.Name != item.Name)
                {
                    return $"Conflicting {role} definition '{item.Id}' in business context '{scenario.Business!.Id}'.".ToFailure<IReadOnlyList<TItem>>();
                }
            }
        }
        return Result<IReadOnlyList<TItem>>.Success(merged);
    }
}
