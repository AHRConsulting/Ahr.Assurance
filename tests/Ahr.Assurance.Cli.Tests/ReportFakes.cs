using Ahr.Assurance.Core;
using Ahr.Assurance.Reporting;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli.Tests;

internal static class ReportFakes
{
    internal static VerificationEvidence Evidence(string id, string correlation) => new(id, correlation, "producer", "test-" + id, "Test " + id,
        VerificationExecutionState.Completed, new(new("Journey", DateTimeOffset.UnixEpoch, TimeSpan.Zero,
        [new("journey", "Journey", null, [new("Satisfied", StepKind.Then, new(AssuranceStatus.Satisfied, "Observed"), TimeSpan.Zero)])]), []), []);

    internal sealed class FakeFiles : IReportFiles
    {
        internal Dictionary<string, string> Json { get; } = new(StringComparer.Ordinal);
        internal Exception? Failure { get; set; }
        internal CancellationTokenSource? CancelOnFailure { get; set; }
        internal void Add(string path, VerificationEvidence evidence) => Json[FullPath(path)] = EvidenceDocumentJson.Serialize(evidence);
        public string FullPath(string path) => "/" + path.TrimStart('/');
        public bool DirectoryExists(string path) => false;
        public string[] JsonFiles(string directory) => throw new InvalidOperationException("No directories in this fake.");
        public string[] Directories(string directory) => throw new InvalidOperationException("No directories in this fake.");
        public bool IsEmpty(string directory) => true;
        public Task<string> ReadAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Failure is null)
            {
                return Task.FromResult(Json[path]);
            }
            CancelOnFailure?.Cancel();
            throw Failure;
        }
    }

    internal sealed class FakeWriter : IRunReportWriter
    {
        internal int Calls { get; private set; }
        internal SelectedRun? Selection { get; private set; }
        internal CommandError? Failure { get; set; }
        internal OperationCanceledException? Cancellation { get; init; }
        public Task<Result<string, CommandError>> WriteAsync(SelectedRun run, CancellationToken token)
        {
            Calls++;
            Selection = run;
            return Cancellation is not null ? throw Cancellation : Task.FromResult(Failure is null
                ? Result<string, CommandError>.Success("/output/report.html")
                : Result<string, CommandError>.Failure(Failure));
        }
    }
}
