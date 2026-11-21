namespace Ahr.Assurance.Reporting;

internal interface IAssuranceArtifactDestination
{
    string ResolveLocation(string artifactName);

    Task WriteAsync(
        IAssuranceReporter reporter,
        AssuranceReport report,
        string location,
        CancellationToken cancellationToken = default);
}

internal sealed record AssuranceReportOutput
{
    public AssuranceReportOutput(
        string name,
        IAssuranceReporter reporter,
        IAssuranceArtifactDestination destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(destination);
        Name = name;
        Reporter = reporter;
        Destination = destination;
    }

    public string Name { get; }

    public IAssuranceReporter Reporter { get; }

    public IAssuranceArtifactDestination Destination { get; }
}

internal sealed class LocalFileArtifactDestination : IAssuranceArtifactDestination
{
    private readonly string? _explicitPath;

    public LocalFileArtifactDestination(string outputDirectory, string fileExtension = ".html")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ValidateExtension(fileExtension);
        OutputDirectory = Path.GetFullPath(outputDirectory);
        FileExtension = fileExtension;
    }

    private LocalFileArtifactDestination(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _explicitPath = Path.GetFullPath(path);
        OutputDirectory = Path.GetDirectoryName(_explicitPath)
            ?? throw new ArgumentException("An artifact destination directory is required.", nameof(path));
        FileExtension = Path.GetExtension(_explicitPath);
        ValidateExtension(FileExtension);
    }

    public string OutputDirectory { get; }

    public string FileExtension { get; }

    public static LocalFileArtifactDestination ForFile(string path) => new(path);

    public string ResolveLocation(string artifactName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactName);
        if (_explicitPath is not null)
        {
            return _explicitPath;
        }

        string safeName = new([.. artifactName.Take(80).Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-')]);
        return Path.Combine(OutputDirectory, $"{safeName}-{Guid.NewGuid():N}{FileExtension}");
    }

    public Task WriteAsync(
        IAssuranceReporter reporter,
        AssuranceReport report,
        string location,
        CancellationToken cancellationToken = default) =>
        ReportArtifactWriter.WriteAsync(reporter, report, location, cancellationToken);

    private static void ValidateExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        if (extension.Length < 2 || extension[0] != '.' || !extension[1..].All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Use a dot followed by an alphanumeric format extension.", nameof(extension));
        }
    }
}
