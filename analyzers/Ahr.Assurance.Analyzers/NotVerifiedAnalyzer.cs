using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports a scenario built and discarded without VerifyAsync, and a VerifyAsync whose task is discarded (AHRA102).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotVerifiedAnalyzer : DiagnosticAnalyzer
{
    internal const string Kind = "Kind";
    internal const string Unverified = "Unverified";
    internal const string NotAwaited = "NotAwaited";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.NotVerified];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.ExpressionStatement);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        IOperation expression = ((IExpressionStatementOperation)context.Operation).Operation;
        if (expression is not IInvocationOperation invocation)
        {
            return;
        }

        if (AssuranceSymbols.IsScenario(invocation.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.NotVerified, invocation.Syntax.GetLocation(),
                ImmutableDictionary<string, string?>.Empty.Add(Kind, Unverified),
                "This scenario is defined but never verified, so the test checks nothing; end it with VerifyAsync()"));
        }
        else if (AssuranceSymbols.IsVerifyAsync(invocation.TargetMethod))
        {
            context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.NotVerified, invocation.Syntax.GetLocation(),
                ImmutableDictionary<string, string?>.Empty.Add(Kind, NotAwaited),
                "VerifyAsync isn't awaited or returned, so the test can finish before the scenario runs"));
        }
    }
}
