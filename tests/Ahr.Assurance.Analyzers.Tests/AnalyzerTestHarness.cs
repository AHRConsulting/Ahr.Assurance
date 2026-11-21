using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Assurance.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        using Ahr.Assurance.Core;
        using Ahr.Assurance.Execution;
        using Ahr.Assurance.Xunit;

        """;

    private static readonly ImmutableArray<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    ];

    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(DiagnosticAnalyzer analyzer, string source)
    {
        Compilation compilation = (await CreateDocument(new AdhocWorkspace(), source).Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    internal static async Task<string> FixAsync(DiagnosticAnalyzer analyzer, CodeFixProvider provider, string source)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Document document = CreateDocument(new AdhocWorkspace(), source);
        Compilation compilation = (await document.Project.GetCompilationAsync(token))!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(token));
        List<CodeAction> actions = [];
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), token));
        CodeAction action = Assert.Single(actions);
        ApplyChangesOperation apply = Assert.IsType<ApplyChangesOperation>(Assert.Single(await action.GetOperationsAsync(token)));
        Document fixedDocument = apply.ChangedSolution.GetDocument(document.Id)!;
        Compilation fixedCompilation = (await fixedDocument.Project.GetCompilationAsync(token))!;
        Assert.Empty(fixedCompilation.GetDiagnostics(token).Where(item => item.Severity == DiagnosticSeverity.Error));
        Assert.Empty(await fixedCompilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(token));
        return (await fixedDocument.GetTextAsync(token)).ToString()[Usings.Length..];
    }

    internal static async Task<bool> OffersNoFixAsync(DiagnosticAnalyzer analyzer, CodeFixProvider provider, string source)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Document document = CreateDocument(new AdhocWorkspace(), source);
        Compilation compilation = (await document.Project.GetCompilationAsync(token))!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(token));
        List<CodeAction> actions = [];
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), token));
        return actions.Count == 0;
    }

    private static Document CreateDocument(AdhocWorkspace workspace, string source) =>
        workspace.AddProject("Consumer", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest))
            .AddMetadataReferences(_references)
            .AddDocument("Consumer.cs", Usings + source);
}
