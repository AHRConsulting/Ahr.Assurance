using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Assurance.Analyzers;

/// <summary>Suggests making an initialised instance step property of a steps class static (AHRA108).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InstanceStepAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.InstanceStep];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.IsStatic
            || property.SetMethod is not null and not { IsInitOnly: true }
            || !IsStepType(property.Type)
            || !AssuranceSymbols.IsStepsClass(property.ContainingType)
            || !property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(context.CancellationToken))
                .OfType<PropertyDeclarationSyntax>().Any(declaration => declaration.Initializer is not null))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.InstanceStep, property.Locations[0], property.Name));
    }

    private static bool IsStepType(ITypeSymbol type) =>
        AssuranceSymbols.IsStep(type)
        || (type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: > 1 } factory
            && factory.ContainingNamespace?.ToDisplayString() == "System"
            && AssuranceSymbols.IsStep(factory.TypeArguments[factory.TypeArguments.Length - 1]));
}
