using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal static class CommandResult
{
    internal static Result<T, CommandError> Success<T>(T value) => Result<T, CommandError>.Success(value);

    internal static Result<T, CommandError> Failure<T>(CommandError error) => Result<T, CommandError>.Failure(error);

    internal static Result<T, CommandError> Failure<T>(string code, string message, string? detail = null) => Failure<T>(new CommandError(code, message, detail));
}

internal static class CommandText
{
    internal static string ListOrNone(IEnumerable<string> items)
    {
        string[] all = [.. items];
        return all.Length == 0 ? "none" : string.Join(", ", all);
    }
}
