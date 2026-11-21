using System.Collections.Immutable;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Tests.Execution;

public sealed class ExampleRowTests
{
    private const int ShortenedLength = 32 + 1 + 6;

    [Fact]
    public void ArgumentsAreNamedAndFormattedForEveryRow()
    {
        Action<decimal, DayOfWeek, string?, object, string, string> row = (amount, day, missing, anything, text, control) => { };
        var longText = new string('x', 60);

        ImmutableArray<StepParameter> example = Example(row, 1.5m, DayOfWeek.Monday, null, new object(), longText, "tab\there");

        Assert.Equal(["amount", "day", "missing", "anything", "text", "control"], example.Select(value => value.Name));
        Assert.Equal("1.5", example[0].Value);
        Assert.Equal("Monday", example[1].Value);
        Assert.Equal("null", example[2].Value);
        Assert.Equal("#4", example[3].Value);
        Assert.Equal(ShortenedLength, example[4].Value.Length);
        Assert.StartsWith(new string('x', 32) + "…", example[4].Value);
        Assert.DoesNotContain('\t', example[5].Value);
        Assert.Equal("argument7", Example(row, 1m, DayOfWeek.Monday, null, null, "", "", "extra")[6].Name);
    }

    [Fact]
    public void ListArgumentsShowTheirElements()
    {
        Action<string[], List<int>, DayOfWeek?[], int[]> row = (frameworks, counts, days, none) => { };

        ImmutableArray<StepParameter> example = Example(row,
            new[] { "xunit-v3", "playwright" }, new List<int> { 1, 2 }, new DayOfWeek?[] { DayOfWeek.Monday, null }, Array.Empty<int>());

        Assert.Equal(["[xunit-v3, playwright]", "[1, 2]", "[Monday, null]", "[]"], example.Select(value => value.Value));
    }

    [Fact]
    public void ALongListIsShortenedAndKeepsItsLaterElementsApart()
    {
        Action<int[]> row = values => { };

        var thirty = Example(row, Enumerable.Range(1, 30).ToArray())[0].Value;
        var thirtyOne = Example(row, Enumerable.Range(1, 31).ToArray())[0].Value;

        Assert.Equal(ShortenedLength, thirty.Length);
        Assert.StartsWith("[1, 2, 3", thirty);
        Assert.Contains('…', thirty);
        Assert.NotEqual(thirty, thirtyOne);
    }

    [Fact]
    public void AListThatWritesItsOwnTextKeepsIt()
    {
        Action<Tags> row = tags => { };

        Assert.Equal("tags (Tags)", Example(row, new Tags { "a" })[0].Value);
    }

    [Fact]
    public void ListsWithElementsThatCannotBeShownFallBackToTheirPosition()
    {
        Action<object[], object[], int[][], HashSet<int>> row = (anything, itself, nested, unordered) => { };
        var itself = new object[1];
        itself[0] = itself;

        ImmutableArray<StepParameter> example = Example(row, new[] { new object() }, itself, new[] { new[] { 1 } }, new HashSet<int> { 1 });

        Assert.Equal(["#1", "#2", "#3", "#4"], example.Select(value => value.Value));
    }

    private static ImmutableArray<StepParameter> Example(Delegate row, params object?[] arguments) =>
        ExampleRow.From(row.Method.GetParameters(), arguments);

    private sealed class Tags : List<string>
    {
        public override string ToString() => "tags";
    }
}
