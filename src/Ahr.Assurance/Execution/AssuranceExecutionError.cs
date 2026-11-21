using Ahr.Assurance.Core;

namespace Ahr.Assurance.Execution;

internal sealed record AssuranceExecutionError(
    string Code,
    string Message,
    string? ExceptionType = null,
    AssuranceRunResult? PartialRun = null)
{
    internal Exception? Exception { get; init; }
}
