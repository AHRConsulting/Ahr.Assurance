using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports a non-async step lambda whose expression body assigns a task, which makes the step wait for it (AHRA101).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskAssignedInStepAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.TaskAssignedInStep];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var lambda = (LambdaExpressionSyntax)context.Node;
        if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword)
            || lambda.ExpressionBody is not AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
            || !AssuranceSymbols.IsTask(context.SemanticModel.GetTypeInfo(assignment, context.CancellationToken).Type)
            || lambda.Parent is not ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation }
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !AssuranceSymbols.InAssurance(method.ContainingType)
            || context.SemanticModel.GetTypeInfo(lambda, context.CancellationToken).ConvertedType is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
            || !AssuranceSymbols.IsTask(invoke.ReturnType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.TaskAssignedInStep, lambda.GetLocation(), assignment.Left.ToString()));
    }
}
