using HSEQ.Common;
using System;

namespace HSEQ.Domain.DocumentNumbering
{
    // Represents the Revision component (R) of a Document Number.
    //
    // Non-project documents:  A, B, C, ... Z                       (ContentRevision is always null)
    // Project-related docs:   A01, A02, ... A99, B01, B02, ...     (ContentRevision is 1-99, resets on major change)
    //
    // AFC is explicitly out of scope and is not represented here.
    public sealed class RevisionCode
    {
        public DocumentVersion Major { get; }
        public int? ContentRevision { get; }

        private const int MinContentRevision = 1;
        private const int MaxContentRevision = 99;

        private RevisionCode(DocumentVersion major, int? contentRevision)
        {
            Major = major;
            ContentRevision = contentRevision;
        }

        public static RevisionCode Create(DocumentVersion major, int? contentRevision, bool isProjectRelated)
        {
            if (!Enum.IsDefined(typeof(DocumentVersion), major))
                throw new InvalidDocumentCodeException($"Major revision '{major}' is not a valid A-Z value.");

            if (isProjectRelated)
            {
                if (contentRevision is null)
                    throw new InvalidDocumentCodeException("Project-related documents require a content revision (01-99).");

                if (contentRevision < MinContentRevision || contentRevision > MaxContentRevision)
                    throw new InvalidDocumentCodeException($"Content revision must be between {MinContentRevision:00} and {MaxContentRevision}.");
            }
            else if (contentRevision is not null)
            {
                throw new InvalidDocumentCodeException("Non-project documents must not have a content revision.");
            }

            return new RevisionCode(major, contentRevision);
        }

        // The revision a brand-new Document starts at: A (project-related documents start at A01).
        public static RevisionCode Initial(bool isProjectRelated)
        {
            return Create(DocumentVersion.A, isProjectRelated ? MinContentRevision : (int?)null, isProjectRelated);
        }

        // Computes the next revision in sequence.
        // Project-related: A01 -> A02 -> ... -> A99 -> B01 -> B02 -> ...
        // Non-project:     A -> B -> C -> ... -> Z
        // Throws if the major revision would advance past Z - no wraparound is defined by the confirmed rules.
        public RevisionCode Next(bool isProjectRelated)
        {
            if (isProjectRelated)
            {
                if (ContentRevision is null)
                    throw new InvalidDocumentCodeException("Cannot advance a non-project revision using project-related rules.");

                if (ContentRevision < MaxContentRevision)
                    return Create(Major, ContentRevision + 1, isProjectRelated: true);

                var nextMajor = AdvanceMajor(Major);
                return Create(nextMajor, MinContentRevision, isProjectRelated: true);
            }
            else
            {
                if (ContentRevision is not null)
                    throw new InvalidDocumentCodeException("Cannot advance a project-related revision using non-project rules.");

                var nextMajor = AdvanceMajor(Major);
                return Create(nextMajor, null, isProjectRelated: false);
            }
        }

        private static DocumentVersion AdvanceMajor(DocumentVersion current)
        {
            if (current == DocumentVersion.Z)
                throw new InvalidDocumentCodeException("Major revision cannot advance past Z.");

            return (DocumentVersion)((int)current + 1);
        }

        // "A" or "A01"
        public string Format()
        {
            return ContentRevision is null
                ? Major.ToString()
                : $"{Major}{ContentRevision.Value:00}";
        }

        public override string ToString() => Format();
    }
}
