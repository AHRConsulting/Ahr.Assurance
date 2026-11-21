using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests;

public sealed class BusinessGroupingTests
{
    [Theory]
    [InlineData("", "Name", "Description")]
    [InlineData("id", " ", "Description")]
    [InlineData("id", "Name", "")]
    public void ExplicitIdentityRequiresNonblankFields(string id, string name, string description) =>
        _ = Assert.Throws<ArgumentException>(() => BusinessContext.Define(id, name, description));
}
