using System.Net;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Cli.Tests.Specifications;

/// <summary>What a run of the CLI left behind, whether it ran in the test's process or its own.</summary>
public interface IToolRun
{
    /// <summary>The folder the scenario works in. Paths in step names are relative to it.</summary>
    string Root { get; }

    int ExitCode { get; }

    /// <summary>Everything the CLI wrote to the terminal, output and errors.</summary>
    string Said { get; }
}

public sealed class ReportSteps : ScenarioSteps<IToolRun>
{
    private const string ReportLine = "Assurance HTML: ";

    public static ScenarioExpectation<IToolRun> TheCliExitsWith(int code) =>
        CreateAssertionStep($"The CLI exits with {code}", tool =>
        {
            if (tool.ExitCode != code)
            {
                Assert.Fail($"Expected exit code {code}, but the CLI exited with {tool.ExitCode}, saying:\n{tool.Said}");
            }
        });

    public static ScenarioExpectation<IToolRun> TheCliSays(string text) =>
        CreateAssertionStep($"The CLI says '{text}'", tool => Assert.Contains(text, tool.Said));

    public static ScenarioExpectation<IToolRun> TheCliDoesNotSay(string text) =>
        CreateAssertionStep($"The CLI doesn't say '{text}'", tool => Assert.DoesNotContain(text, tool.Said));

    public static ScenarioExpectation<IToolRun> TheReportIsWrittenTo(string path) =>
        CreateAssertionStep($"The report is written to {path}", tool =>
            Assert.Equal(path, Path.GetRelativePath(tool.Root, ReportPath(tool)).Replace('\\', '/')));

    public static ScenarioExpectation<IToolRun> TheReportIsTitled(string title) =>
        CreateAssertionStep($"The report is titled '{title}'", tool => Assert.Contains($"<h1>{WebUtility.HtmlEncode(title)}</h1>", Report(tool)));

    public static ScenarioExpectation<IToolRun> TheReportShows(string text) =>
        CreateAssertionStep($"The report shows '{text}'", tool => Assert.Contains(text, Report(tool)));

    public static ScenarioExpectation<IToolRun> TheReportDoesNotShow(string text) =>
        CreateAssertionStep($"The report doesn't show '{text}'", tool => Assert.DoesNotContain(text, Report(tool)));

    public static ScenarioExpectation<IToolRun> TheReportShowsInOrder(string first, string second) =>
        CreateAssertionStep($"The report shows '{first}' before '{second}'", tool =>
        {
            var html = Report(tool);
            var at = html.IndexOf(first, StringComparison.Ordinal);
            Assert.True(at >= 0, $"The report doesn't show '{first}'.");
            Assert.True(html.IndexOf(second, at + first.Length, StringComparison.Ordinal) > at, $"The report doesn't show '{second}' after '{first}'.");
        });

    /// <summary>The HTML report the CLI says it wrote.</summary>
    public static string ReportPath(IToolRun tool)
    {
        var line = tool.Said.Split('\n').Select(text => text.TrimEnd('\r')).LastOrDefault(text => text.StartsWith(ReportLine, StringComparison.Ordinal));
        return line?[ReportLine.Length..] ?? throw new global::Xunit.Sdk.XunitException($"The CLI wrote no report. It said:\n{tool.Said}");
    }

    public static string Report(IToolRun tool) => File.ReadAllText(ReportPath(tool));
}
