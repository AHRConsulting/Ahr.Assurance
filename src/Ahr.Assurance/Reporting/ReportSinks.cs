using Ahr.Assurance.Reporting.Html;

namespace Ahr.Assurance.Reporting;

/// <summary>Built-in verification outputs. Explicit sink lists replace the defaults.</summary>
public static class ReportSinks
{
    /// <summary>A sink that writes an HTML report for each verification.</summary>
    /// <param name="options">The sink's options, or <see langword="null"/> for the defaults.</param>
    public static IAssuranceEvidenceSink Html(HtmlReportSinkOptions? options = null) => new HtmlReportSink(options);

    /// <summary>A sink that writes each verification's evidence as a JSON file. It is the default sink.</summary>
    /// <param name="options">The sink's options, or <see langword="null"/> for the defaults.</param>
    public static IAssuranceEvidenceSink Json(JsonReportSinkOptions? options = null) => new JsonReportSink(options);
}
