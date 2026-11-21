using System;
using Microsoft.CodeAnalysis;

namespace Ahr.Assurance.Analyzers;

internal static class AssuranceSymbols
{
    internal const string ExecutionNamespace = "Ahr.Assurance.Execution";

    internal static bool InAssurance(ISymbol? symbol) =>
        symbol?.ContainingNamespace?.ToDisplayString() is { } name
        && (name == "Ahr.Assurance" || name.StartsWith("Ahr.Assurance.", StringComparison.Ordinal));

    internal static bool IsExecutionType(ITypeSymbol? type, string name) =>
        type is INamedTypeSymbol named && named.Name == name && named.ContainingNamespace?.ToDisplayString() == ExecutionNamespace;

    internal static bool IsScenario(ITypeSymbol? type, bool? typed = null) =>
        IsExecutionType(type, "Scenario") && (typed is null || ((INamedTypeSymbol)type!).IsGenericType == typed);

    internal static bool IsStep(ITypeSymbol? type) => IsExecutionType(type, "ScenarioAction") || IsExecutionType(type, "ScenarioExpectation");

    internal static bool IsStepsClass(INamedTypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type?.BaseType; current is not null; current = current.BaseType)
        {
            if (IsExecutionType(current, "ScenarioSteps"))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsVerifyAsync(IMethodSymbol? method) => method?.Name == "VerifyAsync" && InAssurance(method.ContainingType);

    internal static bool IsTask(ITypeSymbol? type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "Task" && current.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            {
                return true;
            }
        }

        return false;
    }
}
