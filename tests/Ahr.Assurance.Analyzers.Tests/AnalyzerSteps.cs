using System.Collections.Immutable;
using System.Globalization;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Assurance.Analyzers.Tests;

/// <summary>Test code a team writes, and what an analyzer and its fix make of it.</summary>
public sealed class CodeUnderAnalysis
{
    public string Source { get; set; } = "";

    public ImmutableArray<Diagnostic> Diagnostics { get; set; } = [];

    public string FixedSource { get; set; } = "";

    public bool OffersNoFix { get; set; }

    public string Message => Assert.Single(Diagnostics).GetMessage(CultureInfo.InvariantCulture);
}

public sealed class AnalyzerSteps : ScenarioSteps<CodeUnderAnalysis>
{
    public static ScenarioAction<CodeUnderAnalysis> TestCode(string what, string source) =>
        CreateActionStep($"Test code {what}", code => code.Source = source);

    public static ScenarioAction<CodeUnderAnalysis> TheAnalyzerRuns(DiagnosticAnalyzer analyzer) =>
        CreateActionStep($"The {Rule(analyzer)} analyzer runs", async code =>
            code.Diagnostics = await AnalyzerTestHarness.AnalyzeAsync(analyzer, code.Source));

    public static ScenarioAction<CodeUnderAnalysis> TheFixIsApplied(DiagnosticAnalyzer analyzer, CodeFixProvider fix) =>
        CreateActionStep($"The fix for {Rule(analyzer)} is applied", async code =>
            code.FixedSource = await AnalyzerTestHarness.FixAsync(analyzer, fix, code.Source));

    public static ScenarioAction<CodeUnderAnalysis> TheFixIsAskedFor(DiagnosticAnalyzer analyzer, CodeFixProvider fix) =>
        CreateActionStep($"A fix for {Rule(analyzer)} is asked for", async code =>
            code.OffersNoFix = await AnalyzerTestHarness.OffersNoFixAsync(analyzer, fix, code.Source));

    public static ScenarioExpectation<CodeUnderAnalysis> ItIsReported(string rule, int times = 1) =>
        CreateExpectationStep(times == 1 ? $"{rule} is reported" : $"{rule} is reported {times} times", code =>
            ExpectationResult.Check(code.Diagnostics.Length == times && code.Diagnostics.All(diagnostic => diagnostic.Id == rule),
                $"The analyzer reports {rule}.", Repeat(rule, times), Ids(code.Diagnostics)));

    public static ScenarioExpectation<CodeUnderAnalysis> NothingIsReported { get; } =
        CreateExpectationStep("Nothing is reported", code =>
            ExpectationResult.Check(code.Diagnostics.IsEmpty, "The analyzer reports nothing.", "nothing", Ids(code.Diagnostics)));

    public static ScenarioExpectation<CodeUnderAnalysis> TheMessageSays(string text) =>
        CreateAssertionStep($"The message says \"{text}\"", code => Assert.Contains(text, code.Message));

    public static ScenarioExpectation<CodeUnderAnalysis> TheReportedCodeIs(params string[] spans) =>
        CreateAssertionStep($"It points at {string.Join(" and ", spans.Select(span => $"'{span}'"))}", code =>
            Assert.Equal(spans.Order(), code.Diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)).Order()));

    public static ScenarioExpectation<CodeUnderAnalysis> TheFixedCodeHas(string text) =>
        CreateAssertionStep($"The fixed code has {text}", code => Assert.Contains(text, code.FixedSource));

    public static ScenarioExpectation<CodeUnderAnalysis> TheFixedCodeHasNo(string text) =>
        CreateAssertionStep($"The fixed code has no {text}", code => Assert.DoesNotContain(text, code.FixedSource));

    public static ScenarioExpectation<CodeUnderAnalysis> NoFixIsOffered { get; } =
        CreateAssertionStep("No fix is offered", code => Assert.True(code.OffersNoFix));

    private static string Rule(DiagnosticAnalyzer analyzer) => analyzer.SupportedDiagnostics[0].Id;

    private static string Repeat(string rule, int times) => string.Join(", ", Enumerable.Repeat(rule, times));

    private static string Ids(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.IsEmpty ? "nothing" : string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Id));
}

/// <summary>What the analyzers promise a team writing scenarios.</summary>
public static class AnalyzerRules
{
    public static BusinessContext Mistakes { get; } = BusinessContext.Define("analyzers", "Catch mistakes as the code is written",
            "The analyzers report a scenario that would never run, run in the wrong order, or lose its evidence, while the test is being written, and fix it where they can.")
        .Capability("diagnostics", "Build-time diagnostics")
        .Capability("fixes", "Code fixes")
        .Outcome("caught-early", "A broken scenario is found before the tests run")
        .Build();
}
