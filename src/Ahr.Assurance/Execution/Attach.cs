namespace Ahr.Assurance.Execution;

/// <summary>Adds files and data to the evidence of the scenario step that is currently running, or to the scenario between steps.</summary>
public static class Attach
{
    /// <summary>Records a file's location in the evidence. The file is read when a report is generated, so it can still be
    /// being written when this is called. A file that doesn't exist then is shown as not available.</summary>
    /// <param name="path">The file. It's stored relative to the attachments folder.</param>
    /// <param name="name">The name shown in reports. Defaults to the file name.</param>
    /// <param name="keep">When it is kept. <see cref="AttachmentKeep.OnFailure"/> keeps it only when the scenario doesn't pass.</param>
    /// <exception cref="InvalidOperationException">No scenario is running.</exception>
    public static void File(string path, string? name = null, AttachmentKeep keep = AttachmentKeep.Always)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var displayName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name;
        AttachmentScope.Current.AddFile(displayName, AttachmentCapture.MediaType(path), path, keep);
    }

    /// <summary>Adds binary content such as a screenshot.</summary>
    /// <param name="name">The name shown in reports.</param>
    /// <param name="content">The content, written to the attachments folder in the background. Don't change it after this call.</param>
    /// <param name="mediaType">The media type, for example <c>image/png</c>.</param>
    /// <param name="keep">When it is kept. <see cref="AttachmentKeep.OnFailure"/> keeps it only when the scenario doesn't pass.</param>
    /// <exception cref="InvalidOperationException">No scenario is running.</exception>
    public static void Bytes(string name, byte[] content, string mediaType, AttachmentKeep keep = AttachmentKeep.Always)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        AttachmentScope.Current.AddContent(name, mediaType, destination => System.IO.File.WriteAllBytes(destination, content), keep);
    }

    /// <summary>Adds text such as a log or a response body.</summary>
    /// <param name="name">The name shown in reports. Its extension is kept, for example <c>response.json</c>.</param>
    /// <param name="text">The text, kept in full.</param>
    /// <param name="keep">When it is kept. <see cref="AttachmentKeep.OnFailure"/> keeps it only when the scenario doesn't pass.</param>
    /// <exception cref="InvalidOperationException">No scenario is running.</exception>
    public static void Text(string name, string text, AttachmentKeep keep = AttachmentKeep.Always)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(text);
        AttachmentScope.Current.AddContent(name, Path.GetExtension(name).Length == 0 ? "text/plain" : AttachmentCapture.MediaType(name),
            destination => System.IO.File.WriteAllText(destination, text), keep);
    }
}
