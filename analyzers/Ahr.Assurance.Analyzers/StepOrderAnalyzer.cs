using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports a scenario chain, from Define to VerifyAsync, whose steps are out of order, have no When, or end without a check (AHRA105).
/// A scenario can start at its When, and can end with a throws step, which checks what its work threw.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StepOrderAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] _phases = ["Given", "When", "Then"];
    private const int When = 1;
    private const int Then = 2;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.StepOrder];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var verify = (IInvocationOperation)context.Operation;
        if (!AssuranceSymbols.IsVerifyAsync(verify.TargetMethod) || Steps(verify) is not { } steps)
        {
            return;
        }

        List<int> phases = [];
        foreach (var step in steps.Select(step => step.Phase))
        {
            if (step == "And")
            {
                if (phases.Count == 0)
                {
                    Report(context, verify, "starts with And");
                    return;
                }

                phases.Add(phases[phases.Count - 1]);
            }
            else
            {
                phases.Add(System.Array.IndexOf(_phases, step));
            }
        }

        for (var index = 1; index < phases.Count; index++)
        {
            if (!MayFollow(phases[index - 1], phases[index]))
            {
                Report(context, verify, $"has a {_phases[phases[index]]} step after a {_phases[phases[index - 1]]} step");
                return;
            }
        }

        if (!phases.Contains(When))
        {
            Report(context, verify, "has no When step");
            return;
        }

        if (phases[0] == Then)
        {
            Report(context, verify, "starts with a Then step");
            return;
        }

        if (phases[phases.Count - 1] != Then && !steps[steps.Count - 1].MayCheck)
        {
            Report(context, verify, "has no Then step after its last When step");
        }
    }

    private static bool MayFollow(int previous, int next) => next >= previous || (previous == Then && next == When);

    private static List<(string Phase, bool MayCheck)>? Steps(IInvocationOperation verify)
    {
        List<(string Phase, bool MayCheck)> steps = [];
        IOperation? current = Receiver(verify);
        while (current is IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name == "Define" && AssuranceSymbols.IsScenario(invocation.TargetMethod.ContainingType))
            {
                steps.Reverse();
                return steps;
            }

            var step = Phase(invocation.TargetMethod.Name);
            if ((!_phases.Contains(step) && step != "And")
                || !AssuranceSymbols.InAssurance(invocation.TargetMethod.ContainingType))
            {
                return null;
            }

            steps.Add((step, MayCheck(invocation)));
            current = Receiver(invocation);
        }

        return null;
    }

    private static string Phase(string method) => method switch
    {
        "WhenThrows" or "WhenThrowsAny" => "When",
        "AndThrows" or "AndThrowsAny" => "And",
        _ => method
    };

    private static bool MayCheck(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name.Contains("Throws")
        || invocation.TargetMethod.Parameters.Any(parameter => parameter.Type.Name == "ScenarioAction");

    private static IOperation? Receiver(IInvocationOperation invocation)
    {
        IOperation? receiver = invocation.TargetMethod.IsExtensionMethod ? invocation.Arguments.FirstOrDefault()?.Value : invocation.Instance;
        while (receiver is IConversionOperation conversion)
        {
            receiver = conversion.Operand;
        }

        return receiver;
    }

    private static void Report(OperationAnalysisContext context, IInvocationOperation verify, string problem) =>
        context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.StepOrder, verify.Syntax.GetLocation(), problem));
}
