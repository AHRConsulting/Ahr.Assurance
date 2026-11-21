using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Ahr.Assurance.Analyzers;

/// <summary>Makes a step property static and points every use at its type (AHRA108).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InstanceStepCodeFixProvider))]
[Shared]
public sealed class InstanceStepCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [AssuranceDiagnostics.InstanceStep.Id];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault() is not { } declaration
            || model?.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } property)
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create("Make the step static",
            token => MakeStaticAsync(context.Document, declaration, property, token), nameof(InstanceStepCodeFixProvider)), context.Diagnostics);
    }

    private static async Task<Solution> MakeStaticAsync(Document document, PropertyDeclarationSyntax declaration, IPropertySymbol property, CancellationToken token)
    {
        Solution solution = document.Project.Solution;
        var typeName = property.ContainingType.Name;
        ILookup<DocumentId, ReferenceLocation> references = (await SymbolFinder.FindReferencesAsync(property, solution, token).ConfigureAwait(false))
            .SelectMany(symbol => symbol.Locations).ToLookup(location => location.Document.Id);
        foreach (DocumentId id in references.Select(group => group.Key).Append(document.Id).Distinct())
        {
            Document referencing = solution.GetDocument(id)!;
            SyntaxNode root = (await referencing.GetSyntaxRootAsync(token).ConfigureAwait(false))!;
            SyntaxNode[] nodes = [.. references[id]
                .Select(reference => root.FindNode(reference.Location.SourceSpan, getInnermostNodeForTie: true).Parent)
                .OfType<MemberAccessExpressionSyntax>()
                .Where(access => access.Expression is not IdentifierNameSyntax identifier || identifier.Identifier.Text != typeName)];
            if (id == document.Id)
            {
                nodes = [.. nodes, root.FindNode(declaration.Span)];
            }

            solution = solution.WithDocumentSyntaxRoot(id, root.ReplaceNodes(nodes, (original, rewritten) => rewritten switch
            {
                PropertyDeclarationSyntax target => MakeStatic(target),
                MemberAccessExpressionSyntax access => access.WithExpression(SyntaxFactory.IdentifierName(typeName).WithTriviaFrom(access.Expression)),
                _ => rewritten
            }));
        }

        return solution;
    }

    private static PropertyDeclarationSyntax MakeStatic(PropertyDeclarationSyntax property)
    {
        var position = property.Modifiers.Count(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword)
            || modifier.IsKind(SyntaxKind.PrivateKeyword) || modifier.IsKind(SyntaxKind.ProtectedKeyword));
        SyntaxToken keyword = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return position == 0 && property.Modifiers.Count == 0
            ? property.WithModifiers(SyntaxFactory.TokenList(keyword.WithLeadingTrivia(property.Type.GetLeadingTrivia()))).WithType(property.Type.WithoutLeadingTrivia())
            : property.WithModifiers(property.Modifiers.Insert(position, keyword));
    }
}
