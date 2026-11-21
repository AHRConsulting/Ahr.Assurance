using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ahr.Assurance.Core;

namespace Ahr.Assurance.Execution;

internal static class ExampleRow
{
    private const int MaximumValueLength = 40;

    internal static ImmutableArray<StepParameter> From(ParameterInfo[] parameters, IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(arguments);
        return [.. arguments.Select((argument, index) => new StepParameter(
            index < parameters.Length && parameters[index].Name is { Length: > 0 } name ? name : $"argument{index + 1}",
            Format(argument, index)))];
    }

    internal static string Id(string scenarioId)
    {
        ImmutableArray<StepParameter> example = StepHost.Current?.Example ?? [];
        return example.IsEmpty ? scenarioId : $"{scenarioId} [{string.Join(", ", example.Select(value => $"{value.Name}: {value.Value}"))}]";
    }

    internal static ScenarioResult Apply(ScenarioResult scenario)
    {
        ImmutableArray<StepParameter> example = StepHost.Current?.Example ?? [];
        if (example.IsEmpty || scenario.OutlineId is not null)
        {
            return scenario;
        }

        return scenario with
        {
            Id = Id(scenario.Id),
            OutlineId = scenario.Id,
            Example = example
        };
    }

    private static string WithType(string value, Type type) =>
        !type.IsGenericType && value.StartsWith(type.Name, StringComparison.Ordinal) ? value : $"{value} ({TypeNames.Short(type)})";

    private static string Format(object? argument, int index) => Shorten(Describe(argument, true) ?? $"#{index + 1}");

    private static string? Describe(object? argument, bool showElements) => argument switch
    {
        null => "null",
        string value => value,
        bool value => value ? "true" : "false",
        Enum value => value.ToString(),
        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
        Type type => TypeNames.Of(type),
        _ when argument.ToString() is { } value && value != argument.GetType().ToString() => WithType(value, argument.GetType()),
        IList values when showElements => Elements(values),
        _ => null
    };

    private static string? Elements(IList values)
    {
        string?[] elements = [.. values.Cast<object?>().Select(value => Describe(value, false))];
        return elements.Contains(null) ? null : $"[{string.Join(", ", elements)}]";
    }

    private static string Shorten(string text)
    {
        if (text.Length <= MaximumValueLength && !text.Any(char.IsControl))
        {
            return text;
        }

        var start = new string([.. text.Take(MaximumValueLength - 8).Select(character => char.IsControl(character) ? ' ' : character)]);
        return string.Concat(start, "…", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..6]);
    }
}
