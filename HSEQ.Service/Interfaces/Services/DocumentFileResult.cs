using System.IO;

namespace HSEQ.Service.Interfaces.Services
{
    // A document's stored file, ready to be streamed to the client. Carries the
    // stream rather than a byte[] so large documents are not buffered in memory;
    // the caller (the controller's FileStreamResult) disposes it.
    public sealed class DocumentFileResult
    {
        public Stream Content { get; }

        // The name the file is stored and offered under - always "<DocumentNumber><ext>",
        // so a downloaded file is self-identifying, including for superseded revisions.
        public string FileName { get; }

        public DocumentFileResult(Stream content, string fileName)
        {
            Content = content;
            FileName = fileName;
        }
    }
}
