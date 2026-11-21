using System.Text.RegularExpressions;
using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Tests.Reporting;

public sealed class StakeholderReportTests
{
    [Fact]
    public void DocumentLoadsOnlyItsOwnAttachmentsAndAdaptsToNarrowScreens()
    {
        BusinessContext business = BusinessContext.Define("documents", "Documents", "Keep documents available").Capability("read", "Read documents")
            .Outcome("o", "Outcome").Risk("r", "Risk").Reference("jira", "DOC-1", new Uri("https://example.com/DOC-1")).Build();
        AssuranceReport report = new(new("Run", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
            [new("a", "Journey", business, [new("Checked result", StepKind.Then, new(AssuranceStatus.Satisfied, "Checked"), TimeSpan.Zero)])]), []);

        var html = new StakeholderReportWriter().Render(report, "Release", [new(0, 0, "Screen", Link: "attachments/screen.png", Image: true)]);

        Assert.Contains("<meta name=\"viewport\"", html);
        Assert.Contains("@media (max-width: 40rem)", html);
        Assert.Contains("default-src 'none'; style-src 'unsafe-inline'; img-src 'self' file:", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotMatch(new Regex("(src|srcset)=\"(?!attachments/)"), html);
        Assert.Contains("href=\"https://example.com/DOC-1\"", html);
    }
}
