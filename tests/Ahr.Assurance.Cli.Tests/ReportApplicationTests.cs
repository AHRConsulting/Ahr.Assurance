namespace Ahr.Assurance.Cli.Tests;

/// <summary>How the executable and the command are built, below what a user sees.</summary>
public sealed class ReportApplicationTests
{
    [Fact]
    public void ExecutableExportsNoTypesAndNoTestFramework()
    {
        Assert.Empty(typeof(ReportApplication).Assembly.GetExportedTypes());
        Assert.DoesNotContain(typeof(ReportApplication).Assembly.GetReferencedAssemblies(), assembly =>
            assembly.Name?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task CancellationEscapesServicesAndIsMappedOnlyAtCommandBoundary(string stage)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        OperationCanceledException expected = new("original cancellation", caller.Token);
        ReportFakes.FakeFiles files = new()
        {
            Failure = stage == "read" ? expected : null,
            CancelOnFailure = caller
        };
        files.Add("a.json", ReportFakes.Evidence("a", "one"));
        ReportFakes.FakeWriter writer = new()
        {
            Cancellation = stage == "write" ? expected : null
        };
        ReportApplication application = new(files, writer);
        ReportRequest request = new(["a.json"], "output", null, false);
        Assert.Same(expected, await Assert.ThrowsAsync<OperationCanceledException>(() =>
            application.RunAsync(request, caller.Token)));
        using StringWriter output = new();
        using StringWriter error = new();
        Assert.Equal(130, await new ReportCommand(application, output, error).RunAsync(request, caller.Token));
        Assert.Contains("canceled", error.ToString());
        Assert.Empty(output.ToString());
    }
}
