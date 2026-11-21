using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Assurance.Analyzers;

/// <summary>Reports scenarios in one project that share a constant name and have no explicit ID (AHRA106).</summary>
/// <remarks>Scenarios in parameterised tests are left out: each row gets its own ID from its arguments.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateNameAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AssuranceDiagnostics.DuplicateName];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            ConcurrentBag<(string Name, Location Location)> names = [];
            start.RegisterOperationAction(operation => Collect(operation, names), OperationKind.Invocation);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (IGrouping<string, (string Name, Location Location)> group in names.GroupBy(item => item.Name, System.StringComparer.Ordinal).Where(group => group.Count() > 1))
                {
                    foreach ((_, Location location) in group.OrderBy(item => item.Location.SourceTree?.FilePath, System.StringComparer.Ordinal).ThenBy(item => item.Location.SourceSpan.Start))
                    {
                        end.ReportDiagnostic(Diagnostic.Create(AssuranceDiagnostics.DuplicateName, location, group.Count(), group.Key));
                    }
                }
            });
        });
    }

    private static void Collect(OperationAnalysisContext context, ConcurrentBag<(string Name, Location Location)> names)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "Define"
            || !AssuranceSymbols.IsScenario(invocation.TargetMethod.ContainingType, typed: false)
            || invocation.Arguments.Any(argument => argument.Parameter?.Name == "id" && argument.ArgumentKind != ArgumentKind.DefaultValue)
            || invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "name")?.Value is not { ConstantValue: { HasValue: true, Value: string name } } nameArgument
            || InParameterisedMethod(context.ContainingSymbol))
        {
            return;
        }

        names.Add((name, nameArgument.Syntax.GetLocation()));
    }

    private static bool InParameterisedMethod(ISymbol? symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: MethodKind.Ordinary } method && method.ContainingSymbol is INamedTypeSymbol)
            {
                return method.Parameters.Length != 0;
            }
        }

        return false;
    }
}
