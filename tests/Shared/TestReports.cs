using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests;

internal static class TestReports
{
    internal static ReportOptions ForFile(string path) => new()
    {
        Sinks = [new ArtifactReportSink(new("HTML", new StakeholderReportWriter(), LocalFileArtifactDestination.ForFile(path)))]
    };

    internal static string TemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ahr-evidence-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        return root;
    }

    internal static ReportOptions Create(
        string directory, IAssuranceReporter? reporter = null, string extension = ".html", bool text = true) =>
        new()
        {
            DefaultOutputDirectory = directory,
            TextOutput = text ? AssuranceTextVerbosity.Standard : AssuranceTextVerbosity.Off,
            Sinks = reporter is null
                ? [new HtmlReportSink()]
                : [new ArtifactReportSink(new("Custom", reporter, new LocalFileArtifactDestination(directory, extension)))]
        };
}
