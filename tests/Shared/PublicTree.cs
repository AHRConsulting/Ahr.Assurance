namespace Ahr.Assurance.Tests;

internal static class PublicTree
{
    internal static string Root { get; } = Find(AppContext.BaseDirectory);

    internal static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string Find(string start)
    {
        for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Ahr.Assurance.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new DirectoryNotFoundException($"No folder above {start} holds Ahr.Assurance.slnx.");
    }
}
