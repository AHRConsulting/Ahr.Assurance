using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests.Specifications;

/// <summary>What Ahr.Assurance promises about running scenarios and describing them.</summary>
public static class ExecutionCapabilities
{
    public static BusinessContext Running { get; } = BusinessContext.Define("running", "Run scenarios faithfully",
            "Each step runs in order and is awaited, every check is evaluated, and the verdict is what the checks found.")
        .Capability("runs", "Scenario runs")
        .Outcome("faithful-verdicts", "A verdict reflects every check, and nothing that didn't run")
        .Build();

    public static BusinessContext Mistakes { get; } = BusinessContext.Define("mistakes", "Catch mistakes before running",
            "A scenario written wrongly is rejected before any of its steps run, and the mistake is explained.")
        .Capability("validation", "Scenario validation")
        .Outcome("early-mistakes", "A mistake is found before it costs a run")
        .Build();

    public static BusinessContext Stopping { get; } = BusinessContext.Define("stopping", "Stop cleanly",
            "An error or a cancellation stops a scenario where it happened, keeps what ran and releases what the scenario holds.")
        .Capability("stops", "Errors and cancellation")
        .Outcome("clean-stops", "A stopped run says where and why, and leaves nothing behind")
        .Build();

    public static BusinessContext Describing { get; } = BusinessContext.Define("describing", "Describe scenarios",
            "The descriptions, keywords, values and ticket references a team writes reach the results and the report as written.")
        .Capability("metadata", "Scenario metadata")
        .Outcome("as-written", "The report says what the team wrote")
        .Build();

    public static BusinessContext Business { get; } = BusinessContext.Define("business-contexts", "Describe the business",
            "A team names the capabilities, outcomes, rules, risks and objectives a scenario serves, with links to its tickets, and a context that is incomplete or contradicts itself is refused when it is built.")
        .Capability("contexts", "Business contexts")
        .Outcome("sound", "Every context in a report is complete, and its links are safe to open")
        .Build();

    public static BusinessContext Observing { get; } = BusinessContext.Define("observing", "Observe each step",
            "Code such as screenshot capture runs after each step and as each scenario ends, attaches to it, and never breaks the scenario.")
        .Capability("observers", "Step observers")
        .Outcome("captured", "What each step left behind is captured without risk to the verdict")
        .Build();
}
