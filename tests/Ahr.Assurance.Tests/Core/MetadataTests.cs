using Ahr.Assurance.Core;

namespace Ahr.Assurance.Tests;

/// <summary>The shape of the business context builder, which the compiler holds to.</summary>
public sealed class MetadataTests
{
    [Fact]
    public void RoleMethodsDoNotAcceptTheBroadBusinessItemBase()
    {
        foreach (System.Reflection.MethodInfo method in typeof(BusinessContextBuilder).GetMethods().Where(method =>
            method.Name is "Capability" or "Outcome" or "Rule" or "Risk" or "Objective"))
        {
            Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(BusinessItem));
        }
    }
}
