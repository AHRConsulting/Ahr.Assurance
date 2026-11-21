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

/// <summary>Makes the method async and awaits its returned verification, so resources outlive the scenario (AHRA103).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DisposedBeforeVerifiedCodeFixProvider))]
[Shared]
public sealed class DisposedBeforeVerifiedCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [AssuranceDiagnostics.DisposedBeforeVerified.Id];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault()
            is not { Body: { } body } method)
        {
            return;
        }

        ReturnStatementSyntax[] returns = [.. body.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .OfType<ReturnStatementSyntax>()];
        if (returns.Any(statement => statement.Expression is null))
        {
            return;
        }

        var keepsValue = method.ReturnType is GenericNameSyntax;
        context.RegisterCodeFix(CodeAction.Create("Make the method async and await", _ =>
        {
            MethodDeclarationSyntax updated = method.ReplaceNodes(returns, (original, _) => keepsValue
                ? original.WithExpression(SyntaxFactory.AwaitExpression(original.Expression!.WithLeadingTrivia(SyntaxFactory.Space)).WithLeadingTrivia(SyntaxFactory.Space))
                : SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(original.Expression!.WithLeadingTrivia(SyntaxFactory.Space)))
                    .WithTriviaFrom(original));
            updated = updated.WithModifiers(updated.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
            return Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(method, updated)));
        }, nameof(DisposedBeforeVerifiedCodeFixProvider)), context.Diagnostics);
    }
}
