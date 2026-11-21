using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests.Specifications;

/// <summary>What Ahr.Assurance promises the teams that use it.</summary>
public static class Capabilities
{
    public static BusinessContext Authoring { get; } = BusinessContext.Define("authoring", "Write scenarios",
            "A team writes a test as the behaviour it guarantees, in Given, When and Then steps that run in order.")
        .Capability("scenarios", "Scenarios in plain test code")
        .Outcome("readable", "Each test reads as the behaviour it guarantees")
        .Build();

    public static BusinessContext Reuse { get; } = BusinessContext.Define("reuse", "Reuse steps",
            "A team writes a step once and uses it in every scenario that needs it, with the values each one uses.")
        .Capability("steps", "Steps classes")
        .Outcome("consistent", "The same behaviour is checked the same way everywhere")
        .Build();

    public static BusinessContext Exceptions { get; } = BusinessContext.Define("exceptions", "Expect exceptions",
            "A team states that an action must fail with a particular exception, and checks that exception.")
        .Capability("throws", "Throws steps")
        .Outcome("rejections-checked", "Rejections are guaranteed as precisely as successes")
        .Build();

    public static BusinessContext Examples { get; } = BusinessContext.Define("examples", "Record examples",
            "A parameterised test records each row it ran with, so the evidence shows exactly what was tried.")
        .Capability("rows", "Example rows")
        .Outcome("traceable", "Every result can be traced to the values that produced it")
        .Build();

    public static BusinessContext Attachments { get; } = BusinessContext.Define("attachments", "Attach evidence",
            "A step attaches files, screenshots and text to the step that made them, so the report shows what was seen.")
        .Capability("attach", "Attachments")
        .Outcome("seen", "A reader sees what each step saw, not only its verdict")
        .Build();

    public static BusinessContext Checks { get; } = BusinessContext.Define("checks", "Check with any assertion library",
            "A team keeps the assertions it already writes; a failed one is a failed check, and anything else stops the scenario.")
        .Capability("assertions", "Assertion capture")
        .Outcome("honest-verdicts", "A failed check and a broken test are never confused")
        .Build();

    public static BusinessContext Results { get; } = BusinessContext.Define("results", "Check what an action returned",
            "A When that returns a value hands it to the checks after it, typed, without a variable to hold it.")
        .Capability("results", "Step results")
        .Outcome("results-checked", "A check reads the result of the action it checks")
        .Build();
}
