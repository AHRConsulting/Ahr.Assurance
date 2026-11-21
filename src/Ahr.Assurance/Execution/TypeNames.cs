namespace Ahr.Assurance.Execution;

internal static class TypeNames
{
    private static readonly Dictionary<Type, string> _keywords = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(void)] = "void",
    };

    internal static string Of(Type type) => Of(type, withContainingTypes: true);

    internal static string Short(Type type) => Of(type, withContainingTypes: false);

    private static string Of(Type type, bool withContainingTypes)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_keywords.TryGetValue(type, out var keyword))
        {
            return keyword;
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!, withContainingTypes) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Of(underlying, withContainingTypes) + "?";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        Type[] arguments = type.IsGenericType ? type.GetGenericArguments() : [];
        var inherited = 0;
        var outer = "";
        if (type.IsNested && type.DeclaringType is { } declaring)
        {
            inherited = declaring.IsGenericTypeDefinition ? declaring.GetGenericArguments().Length : 0;
            if (withContainingTypes)
            {
                outer = Of(inherited == 0 ? declaring : declaring.MakeGenericType(arguments[..inherited])) + ".";
            }
        }

        return outer + Own(type.Name, arguments[inherited..], withContainingTypes);
    }

    private static string Own(string name, Type[] arguments, bool withContainingTypes)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        if (arguments.Length == 0)
        {
            return name;
        }

        return name + "<" + string.Join(", ", arguments.Select(argument => Of(argument, withContainingTypes))) + ">";
    }
}
