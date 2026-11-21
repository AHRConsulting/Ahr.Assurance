using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;

namespace Ahr.Assurance.Tests.Performance;

[CollectionDefinition(nameof(AllocationBudgetCollection), DisableParallelization = true)]
public sealed class AllocationBudgetCollection;

/// <summary>Hard gate from the budgets in CONTRIBUTING.md. Allocation counts are stable, unlike timings on shared runners.</summary>
[Collection(nameof(AllocationBudgetCollection))]
public sealed class AllocationBudgetTests
{
    private const int Iterations = 200;
    private const int Rounds = 5;
    private const long RunBudgetBytes = 2_600;
    private const long VerifyBudgetBytes = 4_350;
    private const string ReleaseOnly = "The budgets are for Release builds; a Debug build allocates more.";
#if DEBUG
    private const bool Optimized = false;
#else
    private const bool Optimized = true;
#endif

    [Fact]
    public async Task RunningAScenarioStaysWithinItsAllocationBudget()
    {
        Assert.SkipUnless(Optimized, ReleaseOnly);
        Scenario<object> scenario = Define();
        var perRun = await MeasureAsync(() => scenario.RunAsync());
        Assert.True(perRun <= RunBudgetBytes, $"RunAsync allocated {perRun} bytes; the budget is {RunBudgetBytes}.");
    }

    [Fact]
    public async Task VerifyingWithoutOutputStaysWithinItsAllocationBudget()
    {
        Assert.SkipUnless(Optimized, ReleaseOnly);
        Scenario<object> scenario = Define();
        ReportOptions options = new()
        {
            DefaultOutputDirectory = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "assurance-budget"),
            Sinks = [],
            TextOutput = AssuranceTextVerbosity.Off
        };
        var perRun = await MeasureAsync(() =>
            VerificationCoordinator.VerifyAsync("Budget", [scenario], options, new VerificationContext()));
        Assert.True(perRun <= VerifyBudgetBytes, $"Verification allocated {perRun} bytes; the budget is {VerifyBudgetBytes}.");
    }

    private static async Task<long> MeasureAsync(Func<Task> action)
    {
        for (var index = 0; index < Iterations; index++)
        {
            await action();
        }
        var lowest = long.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var before = GC.GetTotalAllocatedBytes(precise: true);
            for (var index = 0; index < Iterations; index++)
            {
                await action();
            }
            lowest = Math.Min(lowest, (GC.GetTotalAllocatedBytes(precise: true) - before) / Iterations);
        }
        return lowest;
    }

    private static Scenario<object> Define() =>
        Scenario.Define("Budget journey", () => new object())
            .Given("Ready", _ => { })
            .When("Act", _ => { })
            .Then(new ScenarioExpectation<object>("Check", (_, _) =>
                Task.FromResult(new ExpectationResult(AssuranceStatus.Satisfied, "Confirmed"))));
}
