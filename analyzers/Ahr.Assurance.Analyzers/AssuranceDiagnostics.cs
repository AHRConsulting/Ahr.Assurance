using Microsoft.CodeAnalysis;

namespace Ahr.Assurance.Analyzers;

internal static class AssuranceDiagnostics
{
    private const string Category = "Usage";
    private const string Help = "https://ahrconsulting.github.io/assurance/docs/analyzers.html#";

    internal static DiagnosticDescriptor TaskAssignedInStep { get; } = Rule("AHRA101",
        "A step lambda returns the task it assigns",
        "This step returns the task it assigns to '{0}', so the step waits for it; use a block body to store the task without waiting",
        "A lambda whose body assigns a task returns that task, so it runs as an asynchronous step and the step waits for it. A block body stores the task without returning it.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor NotVerified { get; } = Rule("AHRA102",
        "A scenario is not verified",
        "{0}",
        "A scenario does nothing until VerifyAsync runs it, and the test can finish before an unawaited VerifyAsync completes. Either way the test passes without checking anything.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor DisposedBeforeVerified { get; } = Rule("AHRA103",
        "A resource is disposed before the scenario runs",
        "'{0}' is disposed when this method returns, before the scenario returned here runs; make the method async and await VerifyAsync",
        "Returning VerifyAsync's task from a method with a using declaration or statement disposes the resource on return, while the scenario that uses it is still to run.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor StepOrder { get; } = Rule("AHRA105",
        "A scenario's steps are out of order or incomplete",
        "This scenario {0}",
        "A scenario's steps go Given, When, then Then. The Given steps are optional. After a Then, a When can start another action, which a later Then checks. The last step is a Then, or a throws step, which checks what its work threw. Verification otherwise fails before any step runs.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor DuplicateName { get; } = Rule("AHRA106",
        "Scenarios share a name and have no ID",
        "{0} scenarios in this project are named '{1}' without an explicit ID, so the report cannot tell them apart",
        "A scenario's name is its ID unless one is given. Scenarios with the same ID in one project are reported as one.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor StoredInlineScenario { get; } = Rule("AHRA107",
        "An inline scenario is stored for reuse",
        "'{0}' stores an inline scenario, which can run only once; define it inside each test",
        "An inline scenario captures the test's local state and refuses a second run. Storing one in a field or property shares it between tests.",
        DiagnosticSeverity.Warning);

    internal static DiagnosticDescriptor InstanceStep { get; } = Rule("AHRA108",
        "A step property can be static",
        "Make '{0}' static so every scenario can use it without a steps instance",
        "Steps are immutable definitions. Static step properties are used as Steps.Name everywhere, rather than through an instance in some tests and the type in others.",
        DiagnosticSeverity.Info);

    private static DiagnosticDescriptor Rule(string id, string title, string message, string description, DiagnosticSeverity severity) =>
        new(id, title, message, Category, severity, isEnabledByDefault: true, description: description, helpLinkUri: Help + id.ToLowerInvariant());
}
