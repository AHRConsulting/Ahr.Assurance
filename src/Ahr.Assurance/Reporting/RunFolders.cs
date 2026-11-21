using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class RunFolders
{
    private static int _pruned;

    internal static string Name(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var name = new string([.. runId.Take(160).Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_')]).TrimStart('.');
        return name.Length == 0 ? "_" : name;
    }

    internal static bool IsRun(string directory) => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Any();

    internal static void Prune(string runs, string current, int? keep)
    {
        if (keep is not { } count || Interlocked.Exchange(ref _pruned, 1) == 1 || !Directory.Exists(runs))
        {
            return;
        }
        _ = Result.Try(() => OldRuns(runs, current, count), IsInUse).Tap(old => Array.ForEach(old, RemoveIfFree));
    }

    private static string[] OldRuns(string runs, string current, int count) =>
        [.. Directory.GetDirectories(runs)
            .Where(directory => !string.Equals(Path.GetFileName(directory), current, StringComparison.Ordinal) && IsRun(directory))
            .OrderByDescending(directory => Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Max(File.GetLastWriteTimeUtc))
            .ThenByDescending(directory => directory, StringComparer.Ordinal)
            .Skip(count - 1)];

    private static void RemoveIfFree(string directory) => _ = Result.Try(() => Directory.Delete(directory, true), IsInUse);

    private static bool IsInUse(Exception exception) => exception is IOException or UnauthorizedAccessException;
}
