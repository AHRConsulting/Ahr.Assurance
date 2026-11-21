using System.Collections.Immutable;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Core;

/// <summary>A named business item with a stable ID, such as a capability, outcome, rule, risk or objective.</summary>
public abstract record BusinessItem
{
    private protected BusinessItem(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    /// <summary>The item's stable ID, unique within its kind in one business context.</summary>
    public string Id { get; }

    /// <summary>The item's readable name, shown in reports.</summary>
    public string Name { get; }
}

/// <summary>Something the system lets people do.</summary>
public sealed record Capability : BusinessItem
{
    /// <summary>Creates a capability.</summary>
    /// <param name="id">The capability's stable ID.</param>
    /// <param name="name">The capability's readable name.</param>
    public Capability(string id, string name) : base(id, name) { }
}

/// <summary>A result people expect from a capability.</summary>
public sealed record Outcome : BusinessItem
{
    /// <summary>Creates an outcome.</summary>
    /// <param name="id">The outcome's stable ID.</param>
    /// <param name="name">The outcome's readable name.</param>
    public Outcome(string id, string name) : base(id, name) { }
}

/// <summary>Something that could go wrong and that the scenarios guard against.</summary>
public sealed record Risk : BusinessItem
{
    /// <summary>Creates a risk.</summary>
    /// <param name="id">The risk's stable ID.</param>
    /// <param name="name">The risk's readable name.</param>
    public Risk(string id, string name) : base(id, name) { }
}

/// <summary>A rule that must hold.</summary>
public sealed record BusinessRule : BusinessItem
{
    /// <summary>Creates a business rule.</summary>
    /// <param name="id">The rule's stable ID.</param>
    /// <param name="name">The rule's readable name.</param>
    public BusinessRule(string id, string name) : base(id, name) { }
}

/// <summary>A goal the work serves.</summary>
public sealed record Objective : BusinessItem
{
    /// <summary>Creates an objective.</summary>
    /// <param name="id">The objective's stable ID.</param>
    /// <param name="name">The objective's readable name.</param>
    public Objective(string id, string name) : base(id, name) { }
}

/// <summary>A link to where the work is tracked, such as a ticket. The provider is free text.</summary>
public sealed record ExternalReference
{
    /// <summary>Creates a reference.</summary>
    /// <param name="provider">Where the work is tracked, such as the name of an issue tracker.</param>
    /// <param name="key">The item's key in that provider, such as a ticket number.</param>
    /// <param name="url">An optional absolute http or https link to the item.</param>
    public ExternalReference(string provider, string key, Uri? url = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (url is not null && (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("A reference URL must be absolute http or https.", nameof(url));
        }

        Provider = provider;
        Key = key;
        Url = url;
    }

    /// <summary>Where the work is tracked, such as the name of an issue tracker.</summary>
    public string Provider { get; }

    /// <summary>The item's key in its provider, such as a ticket number.</summary>
    public string Key { get; }

    /// <summary>A link to the item, or null when there is none.</summary>
    public Uri? Url { get; }

    internal static Result Unique(IEnumerable<ExternalReference> references)
    {
        HashSet<(string, string)> seen = [];
        return Checks.Each(references, seen, static (reference, keys) =>
            Checks.That(keys.Add((reference.Provider, reference.Key)), reference, static item => $"Duplicate reference '{item.Provider}:{item.Key}'."));
    }
}

/// <summary>What a group of scenarios protects: its capabilities, the outcomes people expect, the rules that must hold and the risks guarded against.</summary>
public sealed record BusinessContext
{
    internal BusinessContext(
        string description,
        ImmutableArray<Capability> capabilities,
        ImmutableArray<Outcome> outcomes,
        ImmutableArray<BusinessRule> rules,
        ImmutableArray<Risk> risks,
        ImmutableArray<Objective> objectives,
        string? id,
        string? name,
        ImmutableArray<ExternalReference> references)
    {
        References = references;
        Description = description;
        Capabilities = capabilities;
        Outcomes = outcomes;
        Rules = rules;
        Risks = risks;
        Objectives = objectives;
        Id = id;
        Name = name;
    }

    /// <summary>Explicit grouping identity, or null for an ungrouped description-only context.</summary>
    public string? Id { get; }

    /// <summary>The grouping's readable name, or null for an ungrouped description-only context.</summary>
    public string? Name { get; }

    /// <summary>A short description of what the scenarios protect.</summary>
    public string Description { get; }

    /// <summary>The capabilities the scenarios cover. A built context has at least one.</summary>
    public ImmutableArray<Capability> Capabilities { get; }

    /// <summary>The outcomes people expect. A built context has at least one.</summary>
    public ImmutableArray<Outcome> Outcomes { get; }

    /// <summary>The rules that must hold.</summary>
    public ImmutableArray<BusinessRule> Rules { get; }

    /// <summary>The risks the scenarios guard against.</summary>
    public ImmutableArray<Risk> Risks { get; }

    /// <summary>The goals the work serves.</summary>
    public ImmutableArray<Objective> Objectives { get; }

    /// <summary>Where this work is tracked, such as the tickets the wording came from.</summary>
    public ImmutableArray<ExternalReference> References { get; }

    /// <summary>Starts a business context described by <paramref name="description"/>, without a grouping ID.</summary>
    public static BusinessContextBuilder Define(string description) => new(description);

    /// <summary>Defines a stable business grouping. All occurrences of the ID must agree on name and description.</summary>
    public static BusinessContextBuilder Define(string id, string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(description, id, name);
    }
}

/// <summary>Builds a <see cref="BusinessContext"/>. Each call returns a new builder, and the original stays unchanged.</summary>
public sealed class BusinessContextBuilder
{
    private readonly ImmutableArray<Capability> _capabilities;
    private readonly ImmutableArray<Outcome> _outcomes;
    private readonly ImmutableArray<BusinessRule> _rules;
    private readonly ImmutableArray<Risk> _risks;
    private readonly ImmutableArray<Objective> _objectives;
    private readonly ImmutableArray<ExternalReference> _references;

    internal BusinessContextBuilder(string description, string? id = null, string? name = null)
        : this(description, [], [], [], [], [], [], id, name)
    {
    }

    private BusinessContextBuilder(
        string description,
        ImmutableArray<Capability> capabilities,
        ImmutableArray<Outcome> outcomes,
        ImmutableArray<BusinessRule> rules,
        ImmutableArray<Risk> risks,
        ImmutableArray<Objective> objectives,
        ImmutableArray<ExternalReference> references,
        string? id,
        string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _references = references;
        Description = description;
        _capabilities = capabilities;
        _outcomes = outcomes;
        _rules = rules;
        _risks = risks;
        _objectives = objectives;
        Id = id;
        Name = name;
    }

    internal string Description { get; }

    internal string? Id { get; }

    internal string? Name { get; }

    /// <summary>Adds a capability.</summary>
    public BusinessContextBuilder Capability(Capability capability) =>
        New(capabilities: _capabilities.Add(Require(capability)));

    /// <summary>Adds a capability with the given ID and name.</summary>
    public BusinessContextBuilder Capability(string id, string name) => Capability(new Capability(id, name));

    /// <summary>Adds an expected outcome.</summary>
    public BusinessContextBuilder Outcome(Outcome outcome) =>
        New(outcomes: _outcomes.Add(Require(outcome)));

    /// <summary>Adds an expected outcome with the given ID and name.</summary>
    public BusinessContextBuilder Outcome(string id, string name) => Outcome(new Outcome(id, name));

    /// <summary>Adds a rule that must hold.</summary>
    public BusinessContextBuilder Rule(BusinessRule rule) =>
        New(rules: _rules.Add(Require(rule)));

    /// <summary>Adds a rule with the given ID and name.</summary>
    public BusinessContextBuilder Rule(string id, string name) => Rule(new BusinessRule(id, name));

    /// <summary>Adds a risk the scenarios guard against.</summary>
    public BusinessContextBuilder Risk(Risk risk) =>
        New(risks: _risks.Add(Require(risk)));

    /// <summary>Adds a risk with the given ID and name.</summary>
    public BusinessContextBuilder Risk(string id, string name) => Risk(new Risk(id, name));

    /// <summary>Adds an objective.</summary>
    public BusinessContextBuilder Objective(Objective objective) =>
        New(objectives: _objectives.Add(Require(objective)));

    /// <summary>Adds an objective with the given ID and name.</summary>
    public BusinessContextBuilder Objective(string id, string name) => Objective(new Objective(id, name));

    /// <summary>Adds a link to where the work is tracked.</summary>
    public BusinessContextBuilder Reference(ExternalReference reference) =>
        New(references: _references.Add(Require(reference)));

    /// <summary>Adds a link to where the work is tracked, by provider, key and optional URL.</summary>
    public BusinessContextBuilder Reference(string provider, string key, Uri? url = null) =>
        Reference(new ExternalReference(provider, key, url));

    /// <summary>Builds the business context.</summary>
    /// <exception cref="InvalidOperationException">The context has no capability or no outcome, or holds a duplicate ID or reference.</exception>
    public BusinessContext Build() => TryBuild().OrThrow(static error => new InvalidOperationException(error.Message));

    internal Result<BusinessContext> TryBuild() =>
        Checks.All(this,
                static builder => Checks.That(!builder._capabilities.IsEmpty, "A business context requires at least one capability."),
                static builder => Checks.That(!builder._outcomes.IsEmpty, "A business context requires at least one outcome."),
                static builder => UniqueIds(builder._capabilities, "capability"),
                static builder => UniqueIds(builder._outcomes, "outcome"),
                static builder => UniqueIds(builder._rules, "rule"),
                static builder => UniqueIds(builder._risks, "risk"),
                static builder => UniqueIds(builder._objectives, "objective"),
                static builder => ExternalReference.Unique(builder._references))
            .Bind(() => new BusinessContext(Description, _capabilities, _outcomes, _rules, _risks, _objectives, Id, Name, _references).ToSuccess());

    private BusinessContextBuilder New(
        ImmutableArray<Capability>? capabilities = null,
        ImmutableArray<Outcome>? outcomes = null,
        ImmutableArray<BusinessRule>? rules = null,
        ImmutableArray<Risk>? risks = null,
        ImmutableArray<Objective>? objectives = null,
        ImmutableArray<ExternalReference>? references = null) =>
        new(Description,
            capabilities ?? _capabilities,
            outcomes ?? _outcomes,
            rules ?? _rules,
            risks ?? _risks,
            objectives ?? _objectives,
            references ?? _references,
            Id, Name);

    private static TItem Require<TItem>(TItem? item)
        where TItem : class =>
        item ?? throw new ArgumentNullException(nameof(item));

    private static Result UniqueIds<TItem>(ImmutableArray<TItem> items, string role)
        where TItem : BusinessItem
    {
        if (items.Length < 2)
        {
            return Result.Success();
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        return Checks.Each(items, (Ids: ids, Role: role), static (item, state) =>
            Checks.That(state.Ids.Add(item.Id), (item.Id, state.Role), static duplicate => $"Duplicate {duplicate.Role} id '{duplicate.Id}'."));
    }
}
