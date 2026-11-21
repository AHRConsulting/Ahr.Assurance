using Ahr.Assurance.Core;

namespace Ahr.Assurance.Adapters.Conformance;

/// <summary>What every test framework adapter promises, whichever framework a team uses.</summary>
public static class AdapterBehaviour
{
    public static BusinessContext Verdicts { get; } = BusinessContext.Define("adapter-verdicts", "Verdicts the framework understands",
            "A scenario's verdict reaches the test framework as its own pass or failure, so a team's runner, IDE and CI read it as they read any test.")
        .Capability("native-verdicts", "Native test results")
        .Outcome("same-verdict", "A scenario passes or fails the same way under every framework")
        .Build();

    public static BusinessContext Skips { get; } = BusinessContext.Define("adapter-skips", "Skips the framework understands",
            "A step that skips its test, because an environment it needs is missing, skips it the framework's own way.")
        .Capability("native-skips", "Native skips")
        .Outcome("same-skip", "A skipped scenario is a skipped test under every framework, with its reason kept")
        .Build();

    public static BusinessContext Cancellation { get; } = BusinessContext.Define("adapter-cancellation", "Cancellation",
            "A canceled run stops at once, and the evidence keeps what ran before it.")
        .Capability("cancellation", "Cancellation")
        .Outcome("same-cancellation", "Cancellation behaves the same under every framework")
        .Build();

    public static BusinessContext Delivery { get; } = BusinessContext.Define("adapter-delivery", "Evidence delivery",
            "Evidence that can't be delivered fails the test, without hiding a failed check.")
        .Capability("delivery", "Evidence delivery")
        .Outcome("no-silent-loss", "Evidence is never lost without the test failing")
        .Build();

    public static BusinessContext Identity { get; } = BusinessContext.Define("adapter-identity", "Test identity and output",
            "Evidence names the test it came from, and the test's output shows the scenario when the team wants it.")
        .Capability("identity", "Test identity and output")
        .Outcome("traceable-tests", "Every piece of evidence can be traced to its test")
        .Build();

    public static BusinessContext Isolation { get; } = BusinessContext.Define("adapter-isolation", "Isolated runs",
            "Each verification has its own context and evidence, even when tests run in parallel.")
        .Capability("isolation", "Isolated runs")
        .Outcome("no-crosstalk", "No run sees another run's state")
        .Build();
}
