using HSEQ.Common;

namespace HSEQ.Service.Interfaces.Services
{
    // Result of generating a new Document Number: the assembled string plus the
    // individual pieces DocumentService needs to persist on the Document entity.
    public sealed class GeneratedDocumentNumber
    {
        public string Number { get; }
        public int SerialNumber { get; }
        public DocumentVersion LastVersion { get; }
        public int? ContentRevision { get; }

        public GeneratedDocumentNumber(string number, int serialNumber, DocumentVersion lastVersion, int? contentRevision)
        {
            Number = number;
            SerialNumber = serialNumber;
            LastVersion = lastVersion;
            ContentRevision = contentRevision;
        }
    }
}
