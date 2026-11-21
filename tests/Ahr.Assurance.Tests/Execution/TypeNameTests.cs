using System.Collections.Immutable;
using System.Reflection;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Tests.Execution;

public sealed class TypeNameTests
{
    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(string[]), "string[]")]
    [InlineData(typeof(int[,]), "int[,]")]
    [InlineData(typeof(int?), "int?")]
    [InlineData(typeof(Dictionary<string, int?>), "Dictionary<string, int?>")]
    [InlineData(typeof(ArgumentNullException), "ArgumentNullException")]
    [InlineData(typeof(Outcome<int, long>), "TypeNameTests.Outcome<int, long>")]
    [InlineData(typeof(Left.Options), "TypeNameTests.Left.Options")]
    [InlineData(typeof(Right.Options), "TypeNameTests.Right.Options")]
    [InlineData(typeof(Generic<int>.Inner<string>), "TypeNameTests.Generic<int>.Inner<string>")]
    public void TypesAreWrittenAsInCSharp(Type type, string expected) => Assert.Equal(expected, TypeNames.Of(type));

    [Fact]
    public void RowValuesThatPrintAlikeKeepTheirTypes()
    {
        Assert.Equal("Success(42) (Outcome<int>)", Row(new Outcome<int>(42)));
        Assert.Equal("Success(42) (Outcome<int, long>)", Row(new Outcome<int, long>(42)));
        Assert.Equal("Box { Value = 42 } (Box<int>)", Row(new Box<int>(42)));
        Assert.Equal("Box { Value = 42 } (Box<long>)", Row(new Box<long>(42)));
    }

    [Fact]
    public void RowValuesThatNameTheirTypeAreLeftAsTheyAre()
    {
        Assert.Equal("Money { Amount = 5 }", Row(new Money(5)));
        Assert.Equal("42", Row(42));
        Assert.Equal("ArgumentNullException", Row(typeof(ArgumentNullException)));
    }

    private static string Row(object value) =>
        ExampleRow.From(typeof(TypeNameTests).GetMethod(nameof(Parameter), BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters(), [value])[0].Value;

#pragma warning disable IDE0060, RCS1163
    private static void Parameter(object value)
    {
    }
#pragma warning restore IDE0060, RCS1163

    private sealed class Outcome<T>(T value)
    {
        public override string ToString() => $"Success({value})";
    }

    private sealed class Outcome<T, TError>(T value)
    {
        public override string ToString() => $"Success({value})";
    }

    private sealed record Box<T>(T Value);

    private sealed record Money(decimal Amount);

    private static class Left
    {
        internal sealed class Options;
    }

    private static class Right
    {
        internal sealed class Options;
    }

#pragma warning disable CA1812
    private sealed class Generic<T>
    {
        internal sealed class Inner<TItem>;
    }
#pragma warning restore CA1812
}
