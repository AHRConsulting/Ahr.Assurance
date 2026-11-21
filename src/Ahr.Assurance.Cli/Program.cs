using System.CommandLine;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Cli;

internal static class Program
{
    public static Task<int> Main(string[] args) => RunAsync(args, [new HtmlReportFormat()]);

    internal static async Task<int> RunAsync(string[] args, IReadOnlyList<IReportFormat> formats)
    {
        RootCommand root = new("Render explicitly selected Assurance evidence. Does not run tests or require TRX.");
        Option<string[]> input = new("--input")
        {
            Description = $"Evidence files or directories (top-level JSON only). Defaults to the newest run in the runs folder in {AssuranceConfig.FileName}.",
            Arity = ArgumentArity.OneOrMore
        };
        Option<string?> output = new("--output")
        {
            Description = $"Report destination; must differ from input directories. Defaults to report.output inside the run folder, artifacts/assurance/report beside a solution, or assurance-report with --input."
        };
        Argument<string?> target = new("project-or-solution")
        {
            Description = "A test project, a solution, or a folder holding one. A solution reports the newest run of each of its projects that has " +
                $"{AssuranceConfig.FileName}, in one report. Defaults to this folder.",
            Arity = ArgumentArity.ZeroOrOne
        };
        Option<string[]> project = new("--project")
        {
            Description = "With a solution, report only this project, named by its project file. Can be repeated.",
            Arity = ArgumentArity.OneOrMore
        };
        Option<string[]> excludeProject = new("--exclude-project")
        {
            Description = "With a solution, leave this project out, named by its project file. Can be repeated.",
            Arity = ArgumentArity.OneOrMore
        };
        Option<string?> config = new("--config")
        {
            Description = $"The {AssuranceConfig.FileName} to read. Defaults to the first one found in this folder or a parent folder."
        };
        Option<string?> run = new("--run")
        {
            Description = "Select an exact correlation/run ID."
        };
        Option<bool> details = new("--include-diagnostic-details")
        {
            Description = "Export raw operational error details; may contain sensitive data. Implies --developer."
        };
        Option<bool> developer = new("--developer")
        {
            Description = "Add developer detail to the report and write per-verification evidence copies."
        };
        Option<string?> title = new("--title")
        {
            Description = "Report title, for example a release name. Defaults to report.title in the config, or the name of a single business context."
        };
        Option<string[]> context = new("--context")
        {
            Description = "Show only this business context ID from the evidence. Can be repeated.",
            Arity = ArgumentArity.OneOrMore
        };
        Option<string[]> exclude = new("--exclude-context")
        {
            Description = "Leave out this business context ID. Can be repeated. Replaces report.excludeContexts in the config.",
            Arity = ArgumentArity.OneOrMore
        };
        Option<string?> attachments = new("--attachments")
        {
            Description = "Folder holding the attachments, when it was moved or uploaded apart from the evidence."
        };
        Option<bool> overwrite = new("--overwrite")
        {
            Description = "Allow replacing report files in the output directory."
        };
        Option<string[]> format = new("--format")
        {
            Description = "A report format to write. Can be repeated. Formats other than html write to a folder beside the output named after the format.",
            Arity = ArgumentArity.OneOrMore
        };
        Command report = new("report", "Build one selected run's HTML report, with its attachments in a folder beside it.");
        report.Options.Add(format);
        report.Arguments.Add(target);
        report.Options.Add(input);
        report.Options.Add(project);
        report.Options.Add(excludeProject);
        report.Options.Add(output);
        report.Options.Add(run);
        report.Options.Add(details);
        report.Options.Add(developer);
        report.Options.Add(title);
        report.Options.Add(context);
        report.Options.Add(exclude);
        report.Options.Add(attachments);
        report.Options.Add(overwrite);
        report.Options.Add(config);
        report.SetAction(async (parse, token) =>
        {
            var inputs = parse.GetValue(input) ?? [];
            if (parse.GetValue(target) is { } named && named.StartsWith('-'))
            {
                await Console.Error.WriteLineAsync($"Assurance report failed [usage-error]: Unrecognized option '{named}'.").ConfigureAwait(false);
                return 1;
            }
            ReportCommand command = new(new(new ReportFiles(), new StandardReportWriter(formats)), Console.Out, Console.Error);
            return await ReportTarget.RunAsync(ReportTarget.Resolve(parse.GetValue(target), parse.GetValue(config), parse.GetValue(project) ?? [],
                    parse.GetValue(excludeProject) ?? [], inputs.Length != 0, Environment.CurrentDirectory),
                resolved => command.RunAsync(new(
                    inputs, parse.GetValue(output), parse.GetValue(run),
                    parse.GetValue(overwrite), parse.GetValue(details), parse.GetValue(developer), parse.GetValue(title), parse.GetValue(context),
                    parse.GetValue(attachments), resolved.Config, resolved.Solution, parse.GetValue(format), parse.GetValue(exclude)), token),
                Console.Error).ConfigureAwait(false);
        });
        root.Subcommands.Add(report);

        Option<bool> force = new("--force")
        {
            Description = "Create the file even though this folder has no project file."
        };
        Option<string[]> framework = new("--framework")
        {
            Description = $"Add the Assurance packages for these, at this CLI's version: one test framework, and playwright beside it. Can be repeated, " +
                $"or comma-separated, such as xunit-v3,playwright. One of: {TestFramework.Names}.",
            Arity = ArgumentArity.OneOrMore
        };
        Command init = new("init", $"Create {AssuranceConfig.FileName} in this test project's folder with every default, and say which adapter to add. " +
            "With --framework, also add those packages. In a solution's folder, sets up each test project. An adapter or file already there is left as it is.");
        init.Options.Add(framework);
        init.Options.Add(force);
        init.SetAction(parse => new InitCommand(new DotnetPackageReferences(), Console.Out, Console.Error)
            .Run(Environment.CurrentDirectory, parse.GetValue(force), parse.GetValue(framework)));
        root.Subcommands.Add(init);

        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }
}
