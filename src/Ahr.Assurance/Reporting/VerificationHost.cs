using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Reporting;

internal interface IVerificationHost
{
    CancellationToken CancellationToken { get; }
    VerificationContext Context { get; }
    string Producer { get; }
    Exception CreateFailure(string message, Exception? cause);

    Task RunStepAsync(Func<Task> step) => step();

    ImmutableArray<StepParameter> Example => [];
}

internal static class VerificationHost
{
    internal static async Task<AssuranceVerificationResult> VerifyAsync<TContext>(
        string name, IEnumerable<Scenario<TContext>> scenarios, ReportOptions options,
        IVerificationHost host, CancellationToken cancellationToken = default) where TContext : notnull
    {
        StepHost steps = new(host.RunStepAsync, host.Example);
        VerificationResult result;
        using (StepHost.Enter(steps))
        {
            result = await VerificationCoordinator.VerifyAsync(name, scenarios,
                options with
                {
                    Producer = options.Producer ?? host.Producer
                }, host.Context,
                cancellationToken == default ? host.CancellationToken : cancellationToken).ConfigureAwait(false);
        }

        if (steps.Skip is { } skip)
        {
            ExceptionDispatchInfo.Throw(skip);
        }

        if (!result.Succeeded)
        {
            throw FailureFrom(result, result.Cause ?? steps.Failure, host);
        }
        return new(result.Run, result.Artifacts);
    }

    private static Exception FailureFrom(VerificationResult result, Exception? failedAt, IVerificationHost host)
    {
        Exception failure = host.CreateFailure(string.Join(Environment.NewLine, result.Failures.Select(Line)), result.Cause);
        if (failedAt?.StackTrace is not { } origin)
        {
            return failure;
        }

        return ExceptionDispatchInfo.SetRemoteStackTrace(failure, origin);
    }

    private static string Line(ReportDiagnostic failure)
    {
        if (failure.ExceptionType is { } type)
        {
            return $"[{failure.Code}] {type}: {failure.Message}";
        }
        return $"[{failure.Code}] {failure.Message}";
    }
}
