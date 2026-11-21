using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports a field or property that stores an inline scenario, which can run only once (AHRA107).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StoredInlineScenarioAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.StoredInlineScenario];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        ITypeSymbol? type = context.Symbol switch
        {
            IFieldSymbol { IsImplicitlyDeclared: false } field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };

        if (AssuranceSymbols.IsScenario(type, typed: false) && context.Symbol.Locations.Length != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.StoredInlineScenario, context.Symbol.Locations[0], context.Symbol.Name));
        }
    }
}
