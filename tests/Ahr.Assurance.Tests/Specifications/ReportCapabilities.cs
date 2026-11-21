using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests.Specifications;

/// <summary>What the report promises the people who read it.</summary>
public static class ReportCapabilities
{
    public static BusinessContext Reading { get; } = BusinessContext.Define("report-reading", "Read the report",
            "Someone who doesn't know the code reads what was tested, what passed and what didn't, without being told more than the run shows.")
        .Capability("stakeholder-report", "Stakeholder report")
        .Outcome("understood", "A reader knows what passed, what didn't, and what the run didn't cover")
        .Build();

    public static BusinessContext Areas { get; } = BusinessContext.Define("report-areas", "See results by business area",
            "Journeys that share a business context are shown together as one area, with everything declared for it, and the worst areas first.")
        .Capability("business-groups", "Business groups")
        .Outcome("one-area", "Each area shows everything at stake in it, once")
        .Build();

    public static BusinessContext Attachments { get; } = BusinessContext.Define("report-attachments", "See what each step saw",
            "Screenshots and files appear at the step or journey that made them, and each journey and area says how many it has.")
        .Capability("attachments-shown", "Attachments in the report")
        .Outcome("found", "A reader finds the evidence for a result next to it")
        .Build();

    public static BusinessContext Examples { get; } = BusinessContext.Define("report-examples", "Compare examples",
            "The rows of one parameterised scenario are shown as one journey with a table of its examples.")
        .Capability("examples-table", "Examples table")
        .Outcome("compared", "A reader compares the rows of one scenario side by side")
        .Build();
}
