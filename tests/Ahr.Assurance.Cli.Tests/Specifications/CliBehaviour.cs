using Ahr.Assurance.Core;

namespace Ahr.Assurance.Cli.Tests.Specifications;

/// <summary>What <c>dotnet assurance</c> promises the teams that build their reports with it.</summary>
public static class CliBehaviour
{
    public static BusinessContext Setup { get; } = BusinessContext.Define("tool-setup", "Set up a project for reports",
            "A team runs init once in a test project's or a solution's folder. It writes ahr-assurance.json, and every command finds it from then on.")
        .Capability("init", "dotnet assurance init")
        .Outcome("ready", "A project is ready for reports in one command, and its own settings are never lost")
        .Build();

    public static BusinessContext Adapters { get; } = BusinessContext.Define("adapter-setup", "Add the Assurance adapter",
            "init names the adapter that matches the project's test framework, and --framework adds it, with Playwright beside it if wanted, " +
            "at the CLI's own version.")
        .Capability("init-framework", "dotnet assurance init --framework")
        .Outcome("right-adapter", "Each test project gets the adapter for its framework, in step with the CLI")
        .Rule("explicit", "No package is added unless --framework names it")
        .Rule("no-partial", "A contradiction, an unknown name or a failed package step changes nothing")
        .Risk("wrong-adapter", "xUnit v2 and v3, or MSTest 3 and 4, get the wrong adapter, since their package names don't show which")
        .Build();

    public static BusinessContext Configuration { get; } = BusinessContext.Define("tool-configuration", "Configure reports",
            "A team sets where runs go, how many are kept and what the report is called in ahr-assurance.json, and a mistake in it is named with the file.")
        .Capability("config", "ahr-assurance.json")
        .Outcome("understood", "Every setting is read as written, or the problem is named")
        .Build();

    public static BusinessContext ProjectReports { get; } = BusinessContext.Define("project-reports", "Report a project's runs",
            "After the tests run, a team builds the report of the newest run, or of a run it names, without passing any paths.")
        .Capability("report", "dotnet assurance report")
        .Outcome("right-run", "The report is always of the run the team meant")
        .Build();

    public static BusinessContext SolutionReports { get; } = BusinessContext.Define("solution-reports", "Report a whole solution",
            "One command beside a solution reports every test project that has a config, in one report with a section for each.")
        .Capability("solution-report", "Solution reports")
        .Outcome("one-report", "A release is reviewed from one report")
        .Build();

    public static BusinessContext Trust { get; } = BusinessContext.Define("evidence-trust", "Trust what the report shows",
            "The CLI warns when the evidence could mislead a reader, and shows the verifications in the order they ran.")
        .Capability("warnings", "Warnings about the evidence")
        .Outcome("not-misled", "A reader is never misled by the evidence behind the report")
        .Build();
}
