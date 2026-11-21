using Ahr.Assurance.Execution;
using Ahr.Assurance.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Ahr.Assurance.XunitV2.Tests;

#region snippet: xunit-v2-readme
public sealed class BasketTests(ITestOutputHelper output)
{
    [Fact]
    public Task AnEmptyBasketCostsNothing()
    {
        List<decimal> basket = [];
        decimal total = -1;

        return Scenario.Define("An empty basket costs nothing")
            .Given("An empty basket", basket.Clear)
            .When("The total is worked out", () => total = basket.Sum())
            .Then("It is zero", () => Assert.Equal(0m, total))
            .VerifyAsync(output);
    }
}
#endregion
