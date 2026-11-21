using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal sealed record ProjectFrameworks(string Project, ImmutableArray<TestFramework> Frameworks, Option<TestFramework> Adapter)
{
    internal bool UsesPlaywright { get; init; }

    internal bool HasPlaywright { get; init; }

    private const string MSTestSdk = "MSTest.Sdk";

    private static readonly string[] _xunitV2Packages = ["xunit", "xunit.core"];
    private static readonly string[] _nunitPackages = ["NUnit"];
    private static readonly string[] _mstestPackages = ["MSTest", "MSTest.TestFramework"];
    private static readonly string[] _tUnitPackages = ["TUnit", "TUnit.Core", "TUnit.Engine"];
    private static readonly string[] _playwrightPackages = ["Microsoft.Playwright"];

    internal string Name => Path.GetFileName(Project);

    internal string Folder => Path.GetDirectoryName(Project)!;

    internal bool IsTestProject => Frameworks.Length != 0 || Adapter.IsSome;

    internal static ProjectFrameworks Read(string project)
    {
        var document = XDocument.Load(project);
        var central = PackageVersions.Central(Path.GetDirectoryName(project)!);
        PackageReference[] references = [.. document.Descendants().Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => new PackageReference(Attribute(element, "Include") ?? "", ReferenceVersion(element)))
            .Where(reference => reference.Id.Length != 0)];

        List<TestFramework> frameworks = [];
        AddWhen(frameworks, references.Any(reference => reference.Id.StartsWith("xunit.v3", StringComparison.OrdinalIgnoreCase)), TestFramework.XunitV3);
        AddWhen(frameworks, references.Any(reference => IsOneOf(reference.Id, _xunitV2Packages)), TestFramework.XunitV2);
        AddWhen(frameworks, references.Any(reference => IsOneOf(reference.Id, _nunitPackages)), TestFramework.NUnit);
        foreach (PackageReference reference in references.Where(reference => IsOneOf(reference.Id, _mstestPackages)))
        {
            AddMSTest(frameworks, reference.Version ?? central.Of(reference.Id));
        }
        foreach (var version in SdkVersions(document, project))
        {
            AddMSTest(frameworks, version);
        }
        AddWhen(frameworks, references.Any(reference => IsOneOf(reference.Id, _tUnitPackages)), TestFramework.TUnit);

        Option<TestFramework> adapter = references.Select(reference => TestFramework.ByAdapter(reference.Id)).Values().FirstOrNone();
        return new(project, [.. frameworks.Distinct()], adapter)
        {
            UsesPlaywright = references.Any(reference => IsOneOf(reference.Id, _playwrightPackages)),
            HasPlaywright = references.Any(reference => IsOneOf(reference.Id, [TestFramework.Playwright.Adapter]))
        };
    }

    private static void AddWhen(List<TestFramework> frameworks, bool found, TestFramework framework)
    {
        if (found)
        {
            frameworks.Add(framework);
        }
    }

    private static void AddMSTest(List<TestFramework> frameworks, string? version)
    {
        var major = Major(version);
        AddWhen(frameworks, major is null or >= 4, TestFramework.MSTest);
        AddWhen(frameworks, major is null or 3, TestFramework.MSTestV3);
    }

    private static IEnumerable<string?> SdkVersions(XDocument document, string project)
    {
        XElement? root = document.Root;
        IEnumerable<string> sdks = (Attribute(root, "Sdk") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(document.Descendants().Where(element => element.Name.LocalName == "Sdk")
                .Select(element => Attribute(element, "Name") is { } name ? $"{name}/{Attribute(element, "Version")}" : ""))
            .Concat(document.Descendants().Where(element => element.Name.LocalName == "Import")
                .Select(element => Attribute(element, "Sdk") is { } sdk ? $"{sdk}/{Attribute(element, "Version")}" : ""));
        foreach (var sdk in sdks)
        {
            var parts = sdk.Split('/', 2);
            if (string.Equals(parts[0], MSTestSdk, StringComparison.OrdinalIgnoreCase))
            {
                yield return SdkVersion(parts, project);
            }
        }
    }

    private static string? SdkVersion(string[] parts, string project)
    {
        if (parts.Length == 2 && parts[1].Length != 0)
        {
            return parts[1];
        }
        return GlobalSdkVersion(Path.GetDirectoryName(project)!);
    }

    private static string? GlobalSdkVersion(string folder)
    {
        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "global.json");
            if (File.Exists(path))
            {
                return MSBuildSdk(File.ReadAllText(path));
            }
        }
        return null;
    }

    private static string? MSBuildSdk(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("msbuild-sdks", out JsonElement sdks)
                && sdks.ValueKind == JsonValueKind.Object
                && sdks.TryGetProperty(MSTestSdk, out JsonElement version)
                && version.ValueKind == JsonValueKind.String)
            {
                return version.GetString();
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReferenceVersion(XElement element) =>
        Attribute(element, "VersionOverride")
        ?? Attribute(element, "Version")
        ?? element.Elements().FirstOrDefault(child => child.Name.LocalName is "VersionOverride" or "Version")?.Value;

    internal static int? Major(string? version)
    {
        var text = (version ?? "").Trim().TrimStart('[', '(');
        var digits = new string([.. text.TakeWhile(char.IsAsciiDigit)]);
        if (digits.Length == 0)
        {
            return null;
        }
        return int.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static bool IsOneOf(string id, string[] packages) => packages.Contains(id, StringComparer.OrdinalIgnoreCase);

    private static string? Attribute(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private sealed record PackageReference(string Id, string? Version);

    private sealed class PackageVersions(Dictionary<string, string> versions)
    {
        internal static PackageVersions Central(string folder)
        {
            for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "Directory.Packages.props");
                if (File.Exists(path))
                {
                    return new(XDocument.Load(path).Descendants().Where(element => element.Name.LocalName == "PackageVersion")
                        .Where(element => Attribute(element, "Include") is not null && Attribute(element, "Version") is not null)
                        .GroupBy(element => Attribute(element, "Include")!, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => Attribute(group.Last(), "Version")!, StringComparer.OrdinalIgnoreCase));
                }
            }
            return new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        internal string? Of(string id) => versions.GetValueOrDefault(id);
    }
}
