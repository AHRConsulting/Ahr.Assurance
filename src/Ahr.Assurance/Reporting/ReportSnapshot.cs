using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal sealed record ReportSnapshot(
    string Name,
    DateTimeOffset StartedAt,
    long DurationTicks,
    ImmutableArray<ScenarioSnapshot> Scenarios,
    ImmutableArray<ReportDiagnostic> ExecutionFailures,
    ImmutableArray<ReportDiagnostic> DeliveryFailures)
{
    public static ReportSnapshot Capture(AssuranceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new(report.Run.Name, report.Run.StartedAt, report.Run.Duration.Ticks,
            [.. report.Run.Scenarios.Select(ScenarioSnapshot.Capture)],
            [.. report.Diagnostics.Where(item => !item.Code.StartsWith("report-", StringComparison.Ordinal))],
            [.. report.Diagnostics.Where(item => item.Code.StartsWith("report-", StringComparison.Ordinal))]);
    }

    public Result<AssuranceReport> ToReport() =>
        Check()
            .Bind(() => Scenarios.Traverse(scenario => scenario.ToResult()))
            .Bind(scenarios => Result.Try(() => new AssuranceReport(new(Name, StartedAt, TimeSpan.FromTicks(DurationTicks), [.. scenarios]),
                [.. ExecutionFailures, .. DeliveryFailures], []), EvidenceDocumentJson.IsInvalidValue));

    public Result Check() =>
        Checks.All(this,
            static report => Checks.Text(report.Name, "name"),
            static report => Checks.That(!report.Scenarios.IsDefault && !report.ExecutionFailures.IsDefault && !report.DeliveryFailures.IsDefault &&
                report.DurationTicks >= 0, "Invalid report arrays or duration."),
            static report => Checks.Each(report.Scenarios, 0, static (scenario, _) => CheckScenario(scenario)),
            static report => Checks.Each(report.ExecutionFailures, 0, static (diagnostic, _) => CheckDiagnostic(diagnostic)),
            static report => Checks.Each(report.DeliveryFailures, 0, static (diagnostic, _) => CheckDiagnostic(diagnostic)));

    private static Result CheckScenario(ScenarioSnapshot? scenario) =>
        scenario is null ? Checks.Present(scenario, "scenario") : scenario.ToResult().Match(static _ => Result.Success(), static error => error.ToFailure());

    private static Result CheckDiagnostic(ReportDiagnostic diagnostic) =>
        Checks.All(diagnostic,
            static item => Checks.Present(item, "diagnostic"),
            static item => Checks.Text(item.Code, "code"),
            static item => Checks.Present(item.Message, "message"));
}

internal sealed record BusinessItemSnapshot(string Id, string Name);

internal sealed record ReferenceSnapshot(string Provider, string Key, string? Url)
{
    internal static ImmutableArray<ReferenceSnapshot> Capture(ImmutableArray<ExternalReference> references) =>
        [.. references.Select(reference => new ReferenceSnapshot(reference.Provider, reference.Key, reference.Url?.AbsoluteUri))];

    internal Result<ExternalReference> ToReference() =>
        Url.ToOption().Match(Absolute, () => Create(() => new ExternalReference(Provider, Key)));

    private Result<ExternalReference> Absolute(string url) =>
        AbsoluteUrl(url)
            .ToResult(new Error($"Invalid URL on reference '{Provider}:{Key}'."))
            .Bind(parsed => Create(() => new ExternalReference(Provider, Key, parsed)));

    private static Option<Uri> AbsoluteUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ? Option<Uri>.Some(parsed) : Option<Uri>.None;

    private static Result<ExternalReference> Create(Func<ExternalReference> create) =>
        Result.Try(create, EvidenceDocumentJson.IsInvalidValue);
}

internal sealed record BusinessSnapshot(
    string? Id, string? Name, string Description,
    ImmutableArray<BusinessItemSnapshot> Capabilities,
    ImmutableArray<BusinessItemSnapshot> Outcomes,
    ImmutableArray<BusinessItemSnapshot> Rules,
    ImmutableArray<BusinessItemSnapshot> Risks,
    ImmutableArray<BusinessItemSnapshot> Objectives,
    ImmutableArray<ReferenceSnapshot> References)
{
    internal static BusinessSnapshot Capture(BusinessContext business) =>
        new(business.Id, business.Name, business.Description,
            Items(business.Capabilities), Items(business.Outcomes), Items(business.Rules), Items(business.Risks), Items(business.Objectives),
            ReferenceSnapshot.Capture(business.References));

    internal Result<BusinessContext> ToBusiness() =>
        Checks.That(!Capabilities.IsDefault && !Outcomes.IsDefault && !Rules.IsDefault && !Risks.IsDefault && !Objectives.IsDefault &&
                !References.IsDefault && (Id is null) == (Name is null), "Invalid business metadata.")
            .Bind(() => Checks.All(this,
                static business => Checks.Each(business.Capabilities, 0, static (item, _) => Checks.Present(item, "item")),
                static business => Checks.Each(business.Outcomes, 0, static (item, _) => Checks.Present(item, "item")),
                static business => Checks.Each(business.Rules, 0, static (item, _) => Checks.Present(item, "item")),
                static business => Checks.Each(business.Risks, 0, static (item, _) => Checks.Present(item, "item")),
                static business => Checks.Each(business.Objectives, 0, static (item, _) => Checks.Present(item, "item")),
                static business => Checks.Each(business.References, 0, static (reference, _) => Checks.Present(reference, "reference"))))
            .Bind(() => References.Traverse(static reference => reference.ToReference()))
            .Bind(references => Result.Try(() => Builder(references), EvidenceDocumentJson.IsInvalidValue))
            .Bind(builder => builder.TryBuild());

    private BusinessContextBuilder Builder(IReadOnlyList<ExternalReference> references)
    {
        BusinessContextBuilder builder = Id is null ? BusinessContext.Define(Description) : BusinessContext.Define(Id, Name!, Description);
        builder = Capabilities.Aggregate(builder, (current, item) => current.Capability(item.Id, item.Name));
        builder = Outcomes.Aggregate(builder, (current, item) => current.Outcome(item.Id, item.Name));
        builder = Rules.Aggregate(builder, (current, item) => current.Rule(item.Id, item.Name));
        builder = Risks.Aggregate(builder, (current, item) => current.Risk(item.Id, item.Name));
        builder = Objectives.Aggregate(builder, (current, item) => current.Objective(item.Id, item.Name));
        return references.Aggregate(builder, (current, reference) => current.Reference(reference));
    }

    private static ImmutableArray<BusinessItemSnapshot> Items(IEnumerable<BusinessItem> items) =>
        [.. items.Select(item => new BusinessItemSnapshot(item.Id, item.Name))];
}

internal sealed record StepSnapshot(
    string Name, StepKind Phase, StepKeyword Keyword, string? Description, AssuranceStatus Verdict,
    string Explanation, string? Expected, string? Actual, long DurationTicks, bool ExecutionInterrupted,
    ImmutableArray<StepParameterSnapshot> Parameters);

internal sealed record StepParameterSnapshot(string Name, string Value);

internal sealed record ScenarioSnapshot(
    string Id, string Name, string? Description, BusinessSnapshot? Business, ImmutableArray<ReferenceSnapshot> References,
    ImmutableArray<StepSnapshot> Steps, string? OutlineId, ImmutableArray<StepParameterSnapshot> Example)
{
    internal static ScenarioSnapshot Capture(ScenarioResult scenario) =>
        new(scenario.Id, scenario.Name, scenario.Description, scenario.Business is null ? null : BusinessSnapshot.Capture(scenario.Business),
            ReferenceSnapshot.Capture(scenario.References),
            [.. scenario.Steps.Select(step => new StepSnapshot(step.Name, step.Kind, step.Keyword, step.Description,
                step.Evaluation.Status, step.Evaluation.Explanation, step.Evaluation.Expected, step.Evaluation.Actual, step.Duration.Ticks, step.IsExecutionInterrupted,
                step.Parameters.IsDefault ? [] : [.. step.Parameters.Select(parameter => new StepParameterSnapshot(parameter.Name, parameter.Value))]))],
            scenario.OutlineId,
            scenario.Example.IsDefault ? [] : [.. scenario.Example.Select(value => new StepParameterSnapshot(value.Name, value.Value))]);

    internal Result<ScenarioResult> ToResult() =>
        Checks.All(this,
                static scenario => Checks.Text(scenario.Id, "id"),
                static scenario => Checks.Text(scenario.Name, "name"),
                static scenario => Checks.That(!scenario.Steps.IsDefault && !scenario.References.IsDefault, "Scenario steps are required."),
                static scenario => Checks.That(scenario.HasValidExample(), "A scenario example needs an outline ID and named values."),
                static scenario => Checks.Each(scenario.Steps, 0, static (step, _) => CheckStep(step)))
            .Bind(ReadBusiness)
            .Bind(business => References.Traverse(static reference => reference.ToReference())
                .Bind(references => Result.Try(() => Build(business, references), EvidenceDocumentJson.IsInvalidValue)));

    private Result<Option<BusinessContext>> ReadBusiness() =>
        Business.ToOption().Match(
            static business => business.ToBusiness().Map(static context => Option<BusinessContext>.Some(context)),
            static () => Result<Option<BusinessContext>>.Success(Option<BusinessContext>.None));

    private bool HasValidExample() =>
        !Example.IsDefault && Example.All(IsNamedValue) && (OutlineId is null) == Example.IsEmpty && OutlineId is not { Length: 0 };

    private static bool IsNamedValue(StepParameterSnapshot? value) => value is not null && !string.IsNullOrWhiteSpace(value.Name) && value.Value is not null;

    private static Result CheckStep(StepSnapshot step) =>
        Checks.All(step,
            static item => Checks.Present(item, "step"),
            static item => Checks.Text(item.Name, "name"),
            static item => Checks.Present(item.Explanation, "explanation"),
            static item => Checks.That(Enum.IsDefined(item.Phase) && Enum.IsDefined(item.Keyword) && Enum.IsDefined(item.Verdict) && item.DurationTicks >= 0,
                "Invalid step phase, keyword, verdict, or duration."),
            static item => Checks.That(!item.Parameters.IsDefault && item.Parameters.All(IsNamedValue), "Step parameters need a name and a value."));

    private ScenarioResult Build(Option<BusinessContext> business, IReadOnlyList<ExternalReference> references) =>
        new(Id, Name, business.GetValueOrDefault(), [.. Steps.Select(step => new StepResult(step.Name, step.Phase,
            new(step.Verdict, step.Explanation, step.Expected, step.Actual), TimeSpan.FromTicks(step.DurationTicks),
            step.Description, step.Keyword)
        {
            IsExecutionInterrupted = step.ExecutionInterrupted,
            Parameters = [.. step.Parameters.Select(parameter => new StepParameter(parameter.Name, parameter.Value))]
        })], Description)
        {
            References = [.. references],
            OutlineId = OutlineId,
            Example = [.. Example.Select(value => new StepParameter(value.Name, value.Value))]
        };
}
