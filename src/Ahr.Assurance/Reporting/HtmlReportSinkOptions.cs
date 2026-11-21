namespace Ahr.Assurance.Reporting;

/// <summary>Options for the HTML sink from <see cref="ReportSinks.Html"/>.</summary>
public sealed record HtmlReportSinkOptions
{
    /// <summary>The folder to write HTML files to. A relative path is resolved against <see cref="AppContext.BaseDirectory"/>.
    /// Defaults to <see cref="EvidenceDeliveryContext.DefaultOutputDirectory"/>, the current run's folder.</summary>
    public string? OutputDirectory { get; init; }
    /// <summary>The most characters shown of any one text. Longer text is cut and marked as truncated. Defaults to 4096.</summary>
    public int MaxTextLength { get; init; } = 4096;
}
