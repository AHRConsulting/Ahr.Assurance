using Ahr.Assurance.Execution;

namespace Ahr.Assurance.NUnit.Tests;

public sealed class ReadmeExample
{
    #region snippet: nunit-readme
    [Test]
    public Task AnEmptyBasketCostsNothing()
    {
        List<decimal> basket = [];
        decimal total = -1;

        return Scenario.Define("An empty basket costs nothing")
            .Given("An empty basket", basket.Clear)
            .When("The total is worked out", () => total = basket.Sum())
            .Then("It is zero", () => Assert.That(total, Is.Zero))
            .VerifyAsync();
    }
    #endregion
}
