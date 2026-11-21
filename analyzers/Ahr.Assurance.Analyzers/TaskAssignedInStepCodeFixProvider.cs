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

/// <summary>Gives a task-assigning step lambda a block body (AHRA101).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskAssignedInStepCodeFixProvider))]
[Shared]
public sealed class TaskAssignedInStepCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [AssuranceDiagnostics.TaskAssignedInStep.Id];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault()
            is not { ExpressionBody: { } body } lambda)
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create("Use a block body", _ =>
        {
            BlockSyntax block = SyntaxFactory.Block(
                SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ExpressionStatement(body.WithoutTrivia()).WithTrailingTrivia(SyntaxFactory.Space)),
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken)).WithTriviaFrom(body);
            LambdaExpressionSyntax fixedLambda = lambda.WithExpressionBody(null).WithBlock(block);
            return Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(lambda, fixedLambda)));
        }, nameof(TaskAssignedInStepCodeFixProvider)), context.Diagnostics);
    }
}
