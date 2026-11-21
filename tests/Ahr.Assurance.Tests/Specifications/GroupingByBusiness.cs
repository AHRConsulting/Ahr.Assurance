using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.Tests.Specifications.RenderedReportSteps;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class GroupingByBusiness
{
    [Fact]
    public Task JourneysOfOneContextFormOneGroup() =>
        Scenario.Define<RenderedReport>("Journeys of one business context form one group, with everything declared for it in the order met",
                ReportCapabilities.Areas)
            .Given("A journey for Offline reading that adds a rule", report => report.Add(OfflineReading.Rule("no-web", "No offline web call").Build(), Satisfied))
            .And("A journey for Offline reading that adds a capability, an outcome and a risk", report => report.Add(OfflineReading
                .Capability("guide", "Guide first-time readers").Outcome("guidance", "Connect-first guidance").Risk("missing", "Missing content").Build(), Satisfied))
            .When(TheJourneysAreGrouped)
            .Then(TheJourneysFormGroups(1))
            .And("The group is Offline reading, with both journeys", report =>
            {
                BusinessReportGroup group = Assert.Single(report.Report.BusinessGroups);
                Assert.Equal(("offline", "Offline reading", 2), (group.Business.Id, group.Business.Name, group.Scenarios.Length));
            })
            .And("Its capabilities and outcomes are every journey's, in the order met", report =>
            {
                BusinessContext business = Assert.Single(report.Report.BusinessGroups).Business;
                Assert.Equal(["read", "guide"], business.Capabilities.Select(item => item.Id));
                Assert.Equal(["return", "guidance"], business.Outcomes.Select(item => item.Id));
            })
            .And("Each journey's own context keeps only what it was given", report =>
            {
                _ = Assert.Single(report.Journeys[0].Business!.Capabilities);
                Assert.Empty(report.Journeys[1].Business!.Rules);
            })
            .And("The text summary names the group and counts its journeys", report => Assert.Contains(
                "Business group: Offline reading (offline); 2 associated scenarios", new PlainTextReportWriter().Render(report.Report)))
            .VerifyAsync();

    [Fact]
    public Task ContextsAreNeverMergedByDescription() =>
        Scenario.Define<RenderedReport>("Contexts without an ID are never merged, and different IDs never merge by description", ReportCapabilities.Areas,
                description: "Two teams can describe their areas in the same words; only an ID says they are the same area.")
            .Given("Two journeys with one context that has a description but no ID", report =>
            {
                BusinessContext unidentified = BusinessContext.Define("Shared words").Capability("c", "Capability").Outcome("o", "Outcome").Build();
                report.Add(unidentified, Satisfied);
                report.Add(unidentified, Satisfied);
            })
            .And("A journey whose context has an ID and the same description", report => report.Add(BusinessContext.Define("different", "Name", "Shared words")
                .Capability("c", "Capability").Outcome("o", "Outcome").Build(), Satisfied))
            .And(AJourney(Satisfied))
            .When(TheJourneysAreGrouped)
            .Then(TheJourneysFormGroups(3))
            .And("The context without an ID has no name", report =>
                Assert.Equal((null, null), (report.Journeys[0].Business!.Id, report.Journeys[0].Business!.Name)))
            .VerifyAsync();

    [Theory]
    [InlineData("another name", "Other name", "Description")]
    [InlineData("another description", "Name", "Other description")]
    public Task OneIdWithTwoDefinitionsIsRejected(string difference, string name, string description) =>
        Scenario.Define<RenderedReport>("One ID with two names or two descriptions is rejected", ReportCapabilities.Areas,
                description: "Choosing one would hide that two teams mean different areas.")
            .Given("A journey in the context 'id', named Name and described as Description", report => report.Add(Context("id", "Name", "Description"), Satisfied))
            .And($"A journey in the context 'id' with {difference}", report => report.Add(Context("id", name, description), Satisfied))
            .When(TheGroupingIsRejected)
            .Then<InvalidOperationException>("The rejection says the definitions conflict", (_, rejection) =>
                Assert.Contains("Conflicting definition", rejection.Message))
            .VerifyAsync();

    [Theory]
    [InlineData("capability")]
    [InlineData("outcome")]
    [InlineData("rule")]
    [InlineData("risk")]
    [InlineData("objective")]
    public Task OneItemWithTwoNamesIsRejected(string role) =>
        Scenario.Define<RenderedReport>("A capability, outcome, rule, risk or objective declared twice with different names is rejected", ReportCapabilities.Areas)
            .Given($"A journey whose context declares the {role} 'same' as First", report => report.Add(WithItem(role, "First"), Satisfied))
            .And($"A journey whose context declares the {role} 'same' as Second", report => report.Add(WithItem(role, "Second"), Satisfied))
            .When(TheGroupingIsRejected)
            .Then<InvalidOperationException>($"The rejection names the {role} 'same'", (_, rejection) =>
                Assert.Contains($"Conflicting {role} definition 'same'", rejection.Message))
            .VerifyAsync();

    [Fact]
    public Task SeveralAreasGetASummary() =>
        Scenario.Define<RenderedReport>("Several areas get a summary, with failing areas open and passing ones closed", ReportCapabilities.Areas)
            .Given(AJourneyIn("Checkout", Unsatisfied))
            .And(AJourneyIn("Billing", Satisfied))
            .And(AJourney(Satisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("1 of 3 areas needs attention."))
            .And(TheReportSays("Areas 2 of 3 passed"))
            .And(TheReportSays("Journeys 2 of 3 passed"))
            .And(IsOpen("Checkout"))
            .And(IsClosed("Billing"))
            .And(IsClosed("Other checks"))
            .And(TheReportSays("Checkout ✕ Failed 0 of 1 journey passed"))
            .VerifyAsync();

    [Fact]
    public Task WhenEveryAreaPassesAllAreClosed() =>
        Scenario.Define<RenderedReport>("When every area passes, the headline says so and every area is closed", ReportCapabilities.Areas)
            .Given(AJourneyIn("Checkout", Satisfied))
            .And(AJourneyIn("Billing", Satisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("All 2 areas passed their checks."))
            .And(IsClosed("Checkout"))
            .And(IsClosed("Billing"))
            .And("Nothing is open", report => Assert.DoesNotContain(" open>", report.Html))
            .VerifyAsync();

    [Fact]
    public Task OneAreaStatesItsScopeWhenFiltered() =>
        Scenario.Define<RenderedReport>("A report of one area has no area summary, and says what it covers when filtered", ReportCapabilities.Areas,
                description: "A filtered report must not read as the whole picture.")
            .Given(AJourneyIn("Checkout", Satisfied))
            .And("The report is filtered to Checkout", report => (report.Title, report.Scope) = ("Checkout", "Checkout"))
            .When(TheReportIsRendered)
            .Then("There is no area summary", report => Assert.DoesNotContain("id=\"areas\"", report.Html))
            .And(TheReportSays("The outcome is passing."))
            .And(TheReportSays("This covers only Checkout, and only the checks included in this run. Other areas are not shown."))
            .VerifyAsync();

    [Fact]
    public Task AreasAreListedWorstFirst() =>
        Scenario.Define<RenderedReport>("Areas are listed worst first, then by name, with other checks last among equals", ReportCapabilities.Areas,
                description: "The reader sees what needs attention before what passed.")
            .Given(AJourney(Satisfied))
            .And(AJourneyIn("Zebra", Satisfied))
            .And(AJourneyIn("Alpha", Satisfied))
            .And(AJourneyIn("Unsure", Inconclusive))
            .And(AJourneyIn("Broken", Unsatisfied))
            .When(TheReportIsRendered)
            .Then("The areas are in the order Broken, Unsure, Alpha, Zebra, Other checks", report =>
            {
                int[] positions = [.. new[] { "Broken", "Unsure", "Alpha", "Zebra", "Other checks" }.Select(name => report.PositionOf("<summary>" + name + " "))];
                Assert.Equal(positions.Order(), positions);
            })
            .VerifyAsync();

    private static BusinessContextBuilder OfflineReading =>
        BusinessContext.Define("offline", "Offline reading", "Keep documents available")
            .Capability("read", "Read documents")
            .Outcome("return", "Return saved content");

    private static BusinessContext Context(string id, string name, string description) =>
        BusinessContext.Define(id, name, description).Capability("c", "Capability").Outcome("o", "Outcome").Build();

    private static BusinessContext WithItem(string role, string name)
    {
        BusinessContextBuilder builder = BusinessContext.Define("id", "Name", "Description").Capability("c", "Capability").Outcome("o", "Outcome");
        return (role switch
        {
            "capability" => builder.Capability("same", name),
            "outcome" => builder.Outcome("same", name),
            "rule" => builder.Rule("same", name),
            "risk" => builder.Risk("same", name),
            _ => builder.Objective("same", name)
        }).Build();
    }
}
