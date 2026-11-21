using Ahr.Assurance.Execution;

namespace Ahr.Assurance.MSTest.Tests;

#region snippet: mstest-v3-readme
[TestClass]
public sealed class BasketTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task AnEmptyBasketCostsNothing()
    {
        List<decimal> basket = [];
        decimal total = -1;

        return Scenario.Define("An empty basket costs nothing")
            .Given("An empty basket", basket.Clear)
            .When("The total is worked out", () => total = basket.Sum())
            .Then("It is zero", () => Assert.AreEqual(0m, total))
            .VerifyAsync(TestContext);
    }
}
#endregion
