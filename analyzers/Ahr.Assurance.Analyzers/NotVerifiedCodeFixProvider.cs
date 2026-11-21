using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ahr.Assurance.Analyzers;

/// <summary>Awaits the scenario's verification, in an async method (AHRA102).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NotVerifiedCodeFixProvider))]
[Shared]
public sealed class NotVerifiedCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [AssuranceDiagnostics.NotVerified.Id];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax expression
            || expression.Parent is not ExpressionStatementSyntax statement
            || !InAsyncBody(statement))
        {
            return;
        }

        var verify = diagnostic.Properties.TryGetValue(NotVerifiedAnalyzer.Kind, out var kind) && kind == NotVerifiedAnalyzer.Unverified;
        context.RegisterCodeFix(CodeAction.Create(verify ? "Await VerifyAsync()" : "Await VerifyAsync", _ =>
        {
            ExpressionSyntax target = verify
                ? SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, expression.WithoutTrailingTrivia(), SyntaxFactory.IdentifierName("VerifyAsync")))
                : expression.WithoutTrailingTrivia();
            AwaitExpressionSyntax awaited = SyntaxFactory.AwaitExpression(target.WithLeadingTrivia(SyntaxFactory.Space))
                .WithLeadingTrivia(expression.GetLeadingTrivia()).WithTrailingTrivia(expression.GetTrailingTrivia());
            return Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(expression, awaited)));
        }, verify ? "AwaitVerifyAsync" : "AwaitTask"), diagnostic);
    }

    private static bool InAsyncBody(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(ancestor => ancestor is MethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) switch
        {
            MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.AsyncKeyword),
            LocalFunctionStatementSyntax local => local.Modifiers.Any(SyntaxKind.AsyncKeyword),
            AnonymousFunctionExpressionSyntax function => function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            _ => false
        };
}
