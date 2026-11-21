using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Reporting;

internal static class EvidenceExport
{
    internal static ReportDiagnostic Diagnostic(ReportDiagnostic diagnostic, bool includeDetails) =>
        includeDetails ? diagnostic : diagnostic with
        {
            Message = diagnostic.Code switch
            {
                "invalid-definition" => "The scenario definition is invalid. Operational details withheld.",
                "execution-error" => "Execution failed; evidence may be partial. Operational details withheld.",
                "verification-canceled" => "Verification was canceled; evidence may be partial. Operational details withheld.",
                _ => "The operation failed or could not complete. Operational details withheld."
            },
            ExceptionType = null,
            DetailsWithheld = true
        };

    internal static AssuranceReport Report(AssuranceReport report, bool includeDetails) => report with
    {
        Run = report.Run with
        {
            Scenarios = [.. report.Run.Scenarios.Select(scenario => scenario with
            {
                Steps = [.. scenario.Steps.Select(step => step.IsExecutionInterrupted && !includeDetails
                    ? step with
                    {
                        Evaluation = new(AssuranceStatus.NotEvaluated, step.Evaluation.Explanation.StartsWith(AssuranceRunner.StoppedHereLabel, StringComparison.Ordinal)
                            ? AssuranceRunner.StoppedHereLabel + "; operational details withheld."
                            : "Not run; execution stopped. Operational details withheld.")
                    }
                    : step)]
            })]
        },
        Diagnostics = [.. report.Diagnostics.Select(diagnostic => Diagnostic(diagnostic, includeDetails))]
    };

    internal static VerificationEvidence Project(VerificationEvidence evidence, bool includeDetails) => evidence with
    {
        Report = Report(evidence.Report, includeDetails),
        Attachments = [.. evidence.Attachments.Select(attachment => !attachment.Available && !includeDetails && attachment.Reference != AttachmentCapture.MissingReference
            ? attachment with { Reference = "unavailable", DetailsWithheld = true }
            : attachment)]
    };
}
