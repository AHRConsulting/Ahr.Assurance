using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports returning VerifyAsync's task from a non-async method that disposes a resource on return (AHRA103).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposedBeforeVerifiedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.DisposedBeforeVerified];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Return);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = (IReturnOperation)context.Operation;
        if (operation.ReturnedValue is null
            || context.ContainingSymbol is not IMethodSymbol { IsAsync: false } method
            || !AssuranceSymbols.IsTask(method.ReturnType)
            || !operation.ReturnedValue.DescendantsAndSelf().OfType<IInvocationOperation>().Any(invocation => AssuranceSymbols.IsVerifyAsync(invocation.TargetMethod))
            || DisposedResource(operation.Syntax) is not { } resource)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.DisposedBeforeVerified, operation.Syntax.GetLocation(), resource));
    }

    private static string? DisposedResource(SyntaxNode returned)
    {
        foreach (SyntaxNode ancestor in returned.Ancestors())
        {
            switch (ancestor)
            {
                case UsingStatementSyntax statement:
                    return statement.Declaration?.Variables.FirstOrDefault()?.Identifier.Text ?? statement.Expression?.ToString();
                case BlockSyntax block:
                    LocalDeclarationStatementSyntax? declaration = block.Statements
                        .TakeWhile(statement => !statement.Span.Contains(returned.Span))
                        .OfType<LocalDeclarationStatementSyntax>()
                        .LastOrDefault(statement => statement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));
                    if (declaration is not null)
                    {
                        return declaration.Declaration.Variables.First().Identifier.Text;
                    }

                    break;
                case MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax:
                    return null;
                default:
                    break;
            }
        }

        return null;
    }
}
