using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class DefiningBusinessContexts
{
    [Fact]
    public Task AContextKeepsItsItemsInTheOrderGivenWhateverOrderTheyAreAddedIn() =>
        Scenario.Define("A context keeps each kind of item in the order it was added, whatever order the kinds come in", ExecutionCapabilities.Business)
            .Given("A context that adds an outcome, a risk, two capabilities, another outcome, a rule and an objective", () => { })
            .When("It is built", () => BusinessContext.Define("Describe the business purpose")
                .Outcome("accepted", "An accepted operation")
                .Risk(new("disclosure", "Sensitive content is disclosed"))
                .Capability(new("assure", "Assure access"))
                .Capability("audit", "Audit the decision")
                .Outcome(new("denied", "An ineligible request is denied"))
                .Rule("authenticated", "Require authentication")
                .Objective(new("trust", "Retain customer trust"))
                .Build())
            .Then("It keeps its description", (BusinessContext context) => Assert.Equal("Describe the business purpose", context.Description))
            .And("Its capabilities and outcomes are in the order added", (BusinessContext context) =>
            {
                Assert.Equal(["assure", "audit"], context.Capabilities.Select(item => item.Id));
                Assert.Equal(["accepted", "denied"], context.Outcomes.Select(item => item.Id));
            })
            .And("It has its rule, risk and objective", (BusinessContext context) =>
            {
                Assert.Equal("authenticated", Assert.Single(context.Rules).Id);
                Assert.Equal("disclosure", Assert.Single(context.Risks).Id);
                Assert.Equal("trust", Assert.Single(context.Objectives).Id);
            })
            .VerifyAsync();

    [Fact]
    public Task RulesRisksObjectivesAndReferencesAreOptional() =>
        Scenario.Define("Rules, risks, objectives and references are optional", ExecutionCapabilities.Business)
            .Given("A context with one capability and one outcome", () => { })
            .When("It is built", () => BusinessContext.Define("Required metadata").Capability("capability", "A capability").Outcome("outcome", "An outcome").Build())
            .Then("It has no rules, risks, objectives or references", (BusinessContext context) =>
            {
                Assert.Empty(context.Rules);
                Assert.Empty(context.Risks);
                Assert.Empty(context.Objectives);
                Assert.Empty(context.References);
            })
            .VerifyAsync();

    [Fact]
    public Task AContextNeedsADescription() =>
        Scenario.Define("A context needs a description", ExecutionCapabilities.Business)
            .Given("A blank description", () => { })
            .WhenThrows<ArgumentException>("A context is defined with it", () => BusinessContext.Define(" "))
            .Then("It is refused", (ArgumentException _) => { })
            .VerifyAsync();

    [Theory]
    [InlineData("capability")]
    [InlineData("outcome")]
    public Task AContextNeedsACapabilityAndAnOutcome(string missing) =>
        Scenario.Define("A context needs at least one capability and one outcome", ExecutionCapabilities.Business,
                description: "A report groups scenarios by what they deliver, so a context without them can't be placed.")
            .Given($"A context with no {missing}", () => { })
            .WhenThrows<InvalidOperationException>("It is built", () => WithoutItem(missing).Build())
            .Then("It is refused", (InvalidOperationException _) => { })
            .VerifyAsync();

    [Theory]
    [InlineData("capability", "Duplicate capability id")]
    [InlineData("rule", "Duplicate rule id")]
    public Task TwoItemsOfOneKindCannotShareAnId(string kind, string message) =>
        Scenario.Define("Two items of one kind can't share an ID", ExecutionCapabilities.Business)
            .Given($"A context with two {kind}s whose ID is 'same'", () => { })
            .WhenThrows<InvalidOperationException>("It is built", () => DuplicateIds(kind).Build())
            .Then($"It is refused, saying '{message}'", (InvalidOperationException error) => Assert.Contains(message, error.Message))
            .VerifyAsync();

    [Theory]
    [InlineData("capability with no ID")]
    [InlineData("outcome with a blank name")]
    [InlineData("risk with no ID")]
    [InlineData("rule with no ID")]
    [InlineData("objective with no ID")]
    public Task EveryItemNeedsAnIdAndAName(string item) =>
        Scenario.Define("Every item needs an ID and a name", ExecutionCapabilities.Business)
            .Given($"A {item}", () => { })
            .WhenThrows<ArgumentException>("It is created", () => Item(item))
            .Then("It is refused", (ArgumentException _) => { })
            .VerifyAsync();

    [Fact]
    public Task AnItemEqualsOnlyAnItemOfItsOwnKind() =>
        Scenario.Define("An item equals another with the same ID and name only when they are the same kind", ExecutionCapabilities.Business)
            .Given("Two capabilities and a risk, all with ID 'id' and name 'Name'", () => { })
            .When("They are compared", () => (Capability: new Capability("id", "Name"), Same: new Capability("id", "Name"), Risk: new Risk("id", "Name")))
            .Then("The two capabilities are equal", ((Capability Capability, Capability Same, Risk Risk) items) => Assert.Equal(items.Capability, items.Same))
            .And("The capability and the risk are not", ((Capability Capability, Capability Same, Risk Risk) items) =>
                Assert.NotEqual<BusinessItem>(items.Capability, items.Risk))
            .VerifyAsync();

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.test/ticket")]
    [InlineData("/browse/DOC-1")]
    public Task OnlyAbsoluteHttpLinksAreAccepted(string url) =>
        Scenario.Define("A ticket link must be an absolute http or https address", ExecutionCapabilities.Business,
                description: "The report opens these links, so nothing else is let through.")
            .Given($"A reference to DOC-1 linking to '{url}'", () => { })
            .WhenThrows<ArgumentException>("It is created", () => new ExternalReference("jira", "DOC-1", new Uri(url, UriKind.RelativeOrAbsolute)))
            .Then("It is refused", (ArgumentException _) => { })
            .VerifyAsync();

    [Theory]
    [InlineData("", "DOC-1")]
    [InlineData("jira", " ")]
    public Task AReferenceNeedsAProviderAndAKey(string provider, string key) =>
        Scenario.Define("A ticket reference needs a provider and a key", ExecutionCapabilities.Business)
            .Given($"A reference with provider '{provider}' and key '{key}'", () => { })
            .WhenThrowsAny<ArgumentException>("It is created", () => new ExternalReference(provider, key))
            .Then("It is refused", (ArgumentException _) => { })
            .VerifyAsync();

    [Fact]
    public Task TheSameTicketCannotBeReferencedTwice() =>
        Scenario.Define("The same ticket can't be referenced twice by a context or a scenario", ExecutionCapabilities.Business)
            .Given("A context that references jira DOC-1 twice", () => { })
            .WhenThrows<InvalidOperationException>("It is built", () => BusinessContext.Define("Keep access").Capability("c", "Capability").Outcome("o", "Outcome")
                .Reference("jira", "DOC-1").Reference("jira", "DOC-1").Build())
            .Then("It is refused", (InvalidOperationException _) => { })
            .WhenThrows<InvalidOperationException>("A scenario references jira DOC-1 twice", () =>
                Scenario.Define("Referenced journey").Reference("jira", "DOC-1").Reference("jira", "DOC-1"))
            .Then("It is refused too", (InvalidOperationException _) => { })
            .VerifyAsync();

    [Fact]
    public Task AddingToAContextLeavesTheOriginalAsItWas() =>
        Scenario.Define("Adding to a context makes a new one and leaves the original as it was", ExecutionCapabilities.Business,
                description: "One context can be the start of several.")
            .Given("A context with a capability and an outcome", () => { })
            .When("A reference is added to it", () =>
            {
                BusinessContextBuilder original = BusinessContext.Define("Keep access").Capability("c", "Capability").Outcome("o", "Outcome");
                BusinessContextBuilder added = original.Reference("jira", "DOC-1");
                return (Original: original.Build(), Added: added.Build());
            })
            .Then("The original has no references", ((BusinessContext Original, BusinessContext Added) contexts) => Assert.Empty(contexts.Original.References))
            .And("The new one has the reference", ((BusinessContext Original, BusinessContext Added) contexts) => _ = Assert.Single(contexts.Added.References))
            .VerifyAsync();

    private static BusinessContextBuilder WithoutItem(string missing)
    {
        if (missing == "capability")
        {
            return BusinessContext.Define("Missing capability").Outcome("o", "Outcome");
        }

        return BusinessContext.Define("Missing outcome").Capability("c", "Capability");
    }

    private static BusinessItem Item(string item) => item switch
    {
        "capability with no ID" => new Capability("", "Capability"),
        "outcome with a blank name" => new Outcome("outcome", " "),
        "risk with no ID" => new Risk("", "Risk"),
        "rule with no ID" => new BusinessRule("", "Rule"),
        _ => new Objective("", "Objective")
    };

    private static BusinessContextBuilder DuplicateIds(string kind)
    {
        if (kind == "capability")
        {
            return BusinessContext.Define("Duplicate capability ids").Capability("same", "First").Capability("same", "Second").Outcome("outcome", "Outcome");
        }

        return BusinessContext.Define("Duplicate rule ids").Capability("capability", "Capability").Outcome("outcome", "Outcome").Rule("same", "First").Rule("same", "Second");
    }
}
