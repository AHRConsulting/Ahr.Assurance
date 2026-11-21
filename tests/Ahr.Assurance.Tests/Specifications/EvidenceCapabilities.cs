using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests.Specifications;

/// <summary>What Ahr.Assurance promises about the evidence a verification leaves behind.</summary>
public static class EvidenceCapabilities
{
    public static BusinessContext Delivery { get; } = BusinessContext.Define("evidence-delivery", "Deliver evidence",
            "Every verification publishes its evidence to each sink, and a broken sink or text output never hides a verdict.")
        .Capability("sinks", "Evidence sinks")
        .Outcome("delivered", "The evidence of every run reaches every working sink")
        .Build();

    public static BusinessContext Cancellation { get; } = BusinessContext.Define("evidence-cancellation", "Stop cleanly",
            "A cancelled verification keeps what it saw, delivers it, and rethrows the cancellation the host gave it.")
        .Capability("cancellation", "Cancellation")
        .Outcome("nothing-lost", "A cancelled run still says how far it got")
        .Build();

    public static BusinessContext Privacy { get; } = BusinessContext.Define("evidence-privacy", "Withhold operational details",
            "Reports and logs leave out paths and error details unless a team asks for them; a team's own sinks always get everything.")
        .Capability("export-policy", "Export policy")
        .Outcome("safe-to-share", "A report can be shared without leaking the machine it ran on")
        .Build();

    public static BusinessContext Reports { get; } = BusinessContext.Define("evidence-reports", "Keep reports whole",
            "A report file is replaced only by a complete report, and the run report counts every test that ran.")
        .Capability("report-files", "Report files")
        .Outcome("trustworthy-report", "The report a reader opens is complete and current")
        .Build();

    public static BusinessContext Results { get; } = BusinessContext.Define("evidence-results", "Read results back",
            "A tool loads the evidence a run wrote, and finds its attachments, wherever the folder ends up.")
        .Capability("results", "Assurance results")
        .Outcome("reusable", "Evidence can be read again after the run")
        .Build();
}
