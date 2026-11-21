using System.Text.RegularExpressions;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;
using Ahr.Assurance.Xunit;
using static Ahr.Assurance.Core.AssuranceStatus;
using static Ahr.Assurance.Tests.Specifications.RenderedReportSteps;

namespace Ahr.Assurance.Tests.Specifications;

public sealed class ReadingTheReport
{
    private static BusinessContextBuilder Documents =>
        BusinessContext.Define("documents", "Documents", "Keep documents available").Capability("read", "Read documents");

    [Fact]
    public Task TheHeadlineCountsWhatIsNotPassing() =>
        Scenario.Define<RenderedReport>("The headline says how many risks and outcomes are not passing, and never that it is safe to release",
                ReportCapabilities.Reading,
                description: "A pass shows only that the checks in the run passed, so the report claims nothing more.")
            .Given("A failing journey that guards the risk 'Reader is stranded' and the outcome 'Guidance shown'", report =>
                report.Add(Documents.Outcome("a", "Guidance shown").Risk("r", "Reader is stranded").Build(), Unsatisfied))
            .And("A passing journey for the outcome 'Copy returned'", report => report.Add(Documents.Outcome("o", "Copy returned").Build(), Satisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("2 of 3 risks and outcomes are not passing."))
            .And(TheReportSays("Journeys 1 of 2 passed"))
            .And(TheReportSays("Outcomes 1 of 2 passing"))
            .And("Risks are marked as failing, with 0 of 1 passing", report =>
                Assert.Contains("class=\"role unsatisfied\">✕ <strong>Risks</strong> 0 of 1 passing", report.Html))
            .And(TheReportSays("Risks and outcomes are passing when all linked journeys pass."))
            .And(TheReportDoesNotSay("Rules"))
            .And(TheReportDoesNotSay("Areas"))
            .And(TheReportDoesNotSay("concern"))
            .And(TheReportSays("covers only the checks included in this run"))
            .And(TheReportDoesNotSay("safe to release"))
            .VerifyAsync();

    [Fact]
    public Task OneConcernNotPassingIsCountedInTheSingular() =>
        Scenario.Define<RenderedReport>("The headline counts one concern that isn't passing in the singular", ReportCapabilities.Reading)
            .Given("A failing journey for the outcome 'Guidance shown'", report => report.Add(Documents.Outcome("a", "Guidance shown").Build(), Unsatisfied))
            .And("A passing journey for the outcome 'Copy returned'", report => report.Add(Documents.Outcome("o", "Copy returned").Build(), Satisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("1 of 2 outcomes is not passing."))
            .VerifyAsync();

    [Fact]
    public Task WithoutBusinessContextTheHeadlineCountsJourneys() =>
        Scenario.Define<RenderedReport>("Without a business context, the headline counts journeys", ReportCapabilities.Reading)
            .Given(AJourney(Satisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("1 of 1 journey passed."))
            .And(TheReportDoesNotSay("What is at stake"))
            .And(TheReportDoesNotSay("passing"))
            .VerifyAsync();

    [Fact]
    public Task TheStakeholderViewLeavesOutRawValuesAndIdentifiers() =>
        Scenario.Define<RenderedReport>("The stakeholder view leaves out raw values, identifiers and timings", ReportCapabilities.Reading,
                description: "The reader needs what was checked and how it went, not the values a developer compares.")
            .Given("A failing journey for an outcome with the ID 'outcome-identifier'", report =>
                report.Add(Documents.Outcome("outcome-identifier", "Copy returned").Build(), Unsatisfied))
            .When(TheReportIsRendered)
            .Then(TheReportSays("Checked result"))
            .And(TheReportDoesNotSay("RAW-EXPLANATION"))
            .And(TheReportDoesNotSay("RAW-EXPECTED"))
            .And(TheReportDoesNotSay("RAW-ACTUAL"))
            .And(TheReportDoesNotSay("journey-id"))
            .And(TheReportDoesNotSay("outcome-identifier"))
            .And(TheReportDoesNotSay("Duration"))
            .VerifyAsync();

    [Fact]
    public Task TheDeveloperViewAddsRawValuesAndItsOwnSection() =>
        Scenario.Define<RenderedReport>("The developer view adds the raw values and a section of its own", ReportCapabilities.Reading)
            .Given(AJourney(Unsatisfied))
            .And("The developer view is asked for, with a selected correlation", report => report.Developer = new("<p>Selected correlation</p>"))
            .When(TheReportIsRendered)
            .Then(TheReportSays("RAW-EXPECTED"))
            .And(TheReportSays("Developer details"))
            .And(TheReportSays("Selected correlation"))
            .VerifyAsync();

    [Fact]
    public Task ProblemJourneysAreOpenAndPassingOnesClosed() =>
        Scenario.Define<RenderedReport>("Journeys with a problem are open, and passing ones closed", ReportCapabilities.Reading)
            .Given(AJourney(Unsatisfied))
            .And(AJourney(Satisfied))
            .When(TheReportIsRendered)
            .Then(IsOpen("Journey 1"))
            .And(IsClosed("Journey 2"))
            .VerifyAsync();

    [Theory]
    [InlineData(Satisfied, "satisfied", "✓ Passed")]
    [InlineData(Unsatisfied, "unsatisfied", "✕ Failed")]
    [InlineData(Inconclusive, "inconclusive", "! Inconclusive")]
    [InlineData(NotEvaluated, "notevaluated", "! Not evaluated")]
    public Task EveryVerdictIsShownInWordsAsWellAsColour(AssuranceStatus status, string colour, string label) =>
        Scenario.Define<RenderedReport>("Every verdict is shown in words as well as colour", ReportCapabilities.Reading,
                description: "A reader who can't tell the colours apart, or prints the report, still reads every verdict.")
            .Given(AJourney(status))
            .When(TheReportIsRendered)
            .Then($"The verdict reads '{label}', in the {colour} colour", report =>
                Assert.Contains($"class=\"status {colour}\">{label}</span>", report.Html))
            .VerifyAsync();

    [Theory]
    [InlineData(Unsatisfied, "unsatisfied")]
    [InlineData(Satisfied, "satisfied")]
    public Task TheBannerShowsTheResult(AssuranceStatus status, string colour) =>
        Scenario.Define<RenderedReport>("The banner shows the run's result, and the report says what made it", ReportCapabilities.Reading)
            .Given(AJourney(status))
            .When(TheReportIsRendered)
            .Then($"The banner is in the {colour} colour", report => Assert.Contains($"class=\"banner {colour}\"", report.Html))
            .And("A failed step is highlighted, and only then", report =>
                Assert.Equal(status == Unsatisfied, report.Html.Contains("<li class=\"problem\">", StringComparison.Ordinal)))
            .And("The AHR Assurance logo and name are in the footer, and nothing brands the top of the page", report =>
            {
                Assert.Contains("<footer><svg", report.Html);
                Assert.Contains("aria-label=\"AHR Assurance\"", report.Html);
                Assert.DoesNotContain("AHR ASSURANCE", report.Html);
            })
            .And(TheReportSays("AHR Assurance"))
            .And("It says it is a delivery report, above its own title", report =>
            {
                Assert.Contains("<header><p class=\"kind\">Delivery report</p><h1>", report.Html);
                Assert.Contains(" \u2013 Delivery report</title>", report.Html);
            })
            .And("The version is recorded for tools, not shown to readers", report =>
                Assert.Contains("<meta name=\"generator\" content=\"AHR Assurance ", report.Html))
            .And("It has no separate print layout", report => Assert.DoesNotContain("@media print", report.Html))
            .VerifyAsync();

    [Fact]
    public Task AReportWithNoTitleOfItsOwnSaysSoOnce() =>
        Scenario.Define<RenderedReport>("A report with no title of its own says 'Delivery report' once", ReportCapabilities.Reading)
            .Given("A report with no title of its own", report => report.Title = StakeholderReportWriter.DefaultTitle)
            .And(AJourney(Satisfied))
            .When(TheReportIsRendered)
            .Then("The heading is 'Delivery report', with no label above it", report =>
            {
                Assert.Contains("<header><h1>Delivery report</h1>", report.Html);
                Assert.DoesNotContain("class=\"kind\"", report.Html);
            })
            .And("The page title says it once", report => Assert.Contains("<title>Delivery report</title>", report.Html))
            .VerifyAsync();

    [Fact]
    public Task ConflictingTicketLinksAreReported() =>
        Scenario.Define<RenderedReport>("Two links for one ticket in one area are reported, not chosen between", ReportCapabilities.Reading)
            .Given("Two journeys in Documents that link DOC-1 to different addresses", report =>
            {
                report.Add(Documents.Outcome("o", "Outcome").Reference("jira", "DOC-1", new Uri("https://one.test/")).Build(), Satisfied);
                report.Add(Documents.Outcome("o", "Outcome").Reference("jira", "DOC-1", new Uri("https://two.test/")).Build(), Satisfied);
            })
            .WhenThrows<InvalidOperationException>("The report is rendered", report => report.Render())
            .Then<InvalidOperationException>("The error says the links conflict", (_, error) => Assert.Contains("Conflicting URL", error.Message))
            .VerifyAsync();

    [Fact]
    public Task ALongValueIsCutAndSaysSo() =>
        Scenario.Define<RenderedReport>("A value too long for the report is cut, and says so", ReportCapabilities.Reading)
            .Given("A failing check that expected '123456789'", report => report.Add(null, Unsatisfied, expected: "123456789"))
            .And("A report that keeps 5 characters of a value", report => report.MaxTextLength = 5)
            .And("The developer view is asked for", report => report.Developer = new(""))
            .When(TheReportIsRendered)
            .Then(TheReportSays("12345 [truncated]"))
            .And(TheReportDoesNotSay("123456789"))
            .VerifyAsync();

    [Fact]
    public Task ProjectsGetTheirOwnSectionsWorstFirst() =>
        Scenario.Define<RenderedReport>("Projects get their own sections, worst first, after an overview", ReportCapabilities.Reading)
            .Given(AJourneyIn("Checkout", Satisfied))
            .And(AJourneyIn("Checkout", Unsatisfied))
            .And("The first journey is from Web, tested at 09:00, and the second from Domain, tested at 09:05", report =>
            {
                DateTimeOffset tested = DateTimeOffset.UnixEpoch.AddHours(9);
                report.Projects.AddRange([new("Web", tested, 0, 1), new("Domain", tested.AddMinutes(5), 1, 1)]);
                report.Generated = tested.AddHours(1);
                report.Title = "All";
            })
            .When(TheReportIsRendered)
            .Then(TheReportSays("2 projects. Report generated 1 January 1970, 10:00 UTC"))
            .And(TheReportSays("1 of 2 projects needs attention."))
            .And(TheReportSays("Domain Tested 1 January 1970, 09:05 UTC"))
            .And("The overview links to Domain's section, which comes before Web's", report =>
            {
                Assert.Contains("<a href=\"#project-0\">Domain</a>", report.Html);
                Assert.True(report.PositionOf("<h2>Domain</h2>") < report.PositionOf("<h2>Web</h2>"), "Expected Domain's section before Web's.");
            })
            .And("Each project has its own Checkout area", report =>
            {
                Assert.Contains("id=\"project-0-business-group-0\"", report.Html);
                Assert.Contains("id=\"project-1-business-group-0\"", report.Html);
            })
            .And("What the report covers is said once", report => Assert.Single(Regex.Matches(report.Text, "This covers only")))
            .VerifyAsync();

    [Fact]
    public Task AttachmentsAppearWhereTheyWereMade() =>
        Scenario.Define<RenderedReport>("An attachment appears at the step or journey that made it, and the run's own after every journey",
                ReportCapabilities.Attachments)
            .Given(AJourney(Unsatisfied))
            .And("A screenshot named '<Step image>' from its step, a trace from the journey, and a run log that couldn't be included", report =>
                report.Attachments.AddRange(
                [
                    new(0, 0, "<Step image>", Link: "attachments/step.png", Image: true),
                    new(0, null, "Trace", Link: "attachments/a&b.bin"),
                    new(null, null, "Run log", Problem: "could not be included in this report.")
                ]))
            .And("The report is titled '<Release>'", report => report.Title = "<Release>")
            .When(TheReportIsRendered)
            .Then("The screenshot is shown at its step, with its name as written", report =>
                Assert.Contains("<a href=\"attachments/step.png\"><img src=\"attachments/step.png\" alt=\"&lt;Step image&gt;\"", Steps(report)))
            .And("The trace is linked from the journey, not from a step", report =>
            {
                Assert.DoesNotContain("Trace", Steps(report));
                Assert.Contains("<a href=\"attachments/a&amp;b.bin\">Trace</a>", report.Html);
                Assert.DoesNotContain("<img src=\"attachments/a&amp;b.bin", report.Html);
            })
            .And("The run log comes after the journeys, with why it isn't there", report =>
                Assert.True(report.PositionOf("Run log: could not be included in this report.") > report.PositionOf("</ol>"), "Expected the run log after the journeys."))
            .And(TheReportSays("<Release>"))
            .VerifyAsync();

    [Fact]
    public Task JourneysAndAreasCountTheirAttachments() =>
        Scenario.Define<RenderedReport>("Each journey and area says how many attachments it has", ReportCapabilities.Attachments,
                description: "A closed journey still tells the reader there is evidence inside.")
            .Given(AJourneyIn("Documents", Satisfied))
            .And(AJourneyIn("Documents", Satisfied))
            .And(AJourneyIn("Other", Satisfied))
            .And("A screenshot and a trace from journey 1, a missing log from journey 2, and a run log", report => report.Attachments.AddRange(
            [
                new(0, 0, "Screen", Link: "attachments/screen.png", Image: true),
                new(0, null, "Trace", Link: "attachments/trace.zip"),
                new(1, 0, "Log", Problem: "is not available."),
                new(null, null, "Run log", Link: "attachments/run.log")
            ]))
            .When(TheReportIsRendered)
            .Then(ShowsAttachments("Journey 1", 2))
            .And(ShowsAttachments("Journey 2", 1))
            .And(ShowsAttachments("Journey 3", 0))
            .And(ShowsAttachments("Documents", 3))
            .And(ShowsAttachments("Other", 0))
            .And("The run log is not counted on any journey or area", report => Assert.Equal(3, Regex.Count(report.Html, "class=\"attached\"")))
            .VerifyAsync();

    [Fact]
    public Task RowsOfOneScenarioAreOneJourneyWithATableOfExamples() =>
        Scenario.Define<RenderedReport>("The rows of one scenario are one journey, with a table of its examples", ReportCapabilities.Examples)
            .Given("A passing GBP row of 'A charge is recorded'", report => report.AddRow("A charge is recorded", "GBP", Satisfied))
            .And(AJourney(Satisfied))
            .And("A failing EUR row of 'A charge is recorded'", report => report.AddRow("A charge is recorded", "EUR", Unsatisfied))
            .When(TheReportIsRendered)
            .Then("The rows are one journey", report => Assert.Single(Regex.Matches(report.Html, "class=\"journey outline\"")))
            .And(TheReportSays("1 of 2 examples passed"))
            .And("The examples are in a table", report => Assert.Contains("<table class=\"examples\">", report.Html))
            .And(TheReportSays("Example: currency = EUR"))
            .And("Both rows are shown at the first row's place, before the other journey", report =>
                Assert.True(report.PositionOf("Example: currency = EUR") < report.PositionOf("Journey 2"), "Expected the EUR row before Journey 2."))
            .And("The text summary names each row's values", report =>
                Assert.Contains("Example: currency = GBP", new PlainTextReportWriter().Render(report.Report)))
            .VerifyAsync();

    [Fact]
    public Task AScenarioWithExamplesCountsAsOneJourney() =>
        Scenario.Define<RenderedReport>("A scenario with several examples counts as one journey, which passes only when every example passes",
                ReportCapabilities.Examples,
                description: "The counts match the journeys listed, however many rows a parameterised test has.")
            .Given(AJourneyIn("Payments", Satisfied))
            .And("A passing GBP row and a failing EUR row of 'A charge is recorded' in Payments", report =>
            {
                report.AddRow("A charge is recorded", "GBP", Satisfied, report.Area("Payments"));
                report.AddRow("A charge is recorded", "EUR", Unsatisfied, report.Area("Payments"));
            })
            .And(AJourneyIn("Documents", Satisfied))
            .When(TheReportIsRendered)
            .Then("Payments says 1 of 2 journeys passed", report => Assert.Contains("1 of 2 journeys passed", report.Summary("Payments")))
            .And(TheReportSays("Journeys 2 of 3 passed"))
            .And("The Payments outcome counts 1 of 2 journeys", report => Assert.Contains("data-label=\"Journeys\">1 of 2 passed", report.Html))
            .And("The outcome lists the failing scenario once", report =>
                Assert.Single(Regex.Matches(report.Html, "<li><a href=\"#journey-\\d+\">A charge is recorded</a></li>")))
            .VerifyAsync();

    private static string Steps(RenderedReport report) =>
        report.Html[report.PositionOf("<ol>")..report.PositionOf("</ol>")];
}
