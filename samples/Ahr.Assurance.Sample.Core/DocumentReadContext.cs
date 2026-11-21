using Ahr.Assurance.Sample.Application;

namespace Ahr.Assurance.Sample;

public class DocumentReadContext
{
    public Document ExpectedDocument { get; } = new("handbook", "Offline-ready field handbook");

    public FakeDocumentSource Source { get; set; } = null!;

    public FakeInMemoryDocumentCache Cache { get; set; } = null!;

    public DocumentReader Reader { get; set; } = null!;

    public DocumentReadResult? Result { get; set; }
}
