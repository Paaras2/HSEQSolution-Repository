using System;

namespace HSEQ.API.Model.RequestModels
{
    // Metadata-only. An update never touches the document's content.
    public class UpdateDocumentRequestModel : BaseUpdateRequestModel
    {
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }

        // Number, Revision, Project, Organizational Management/Activity classification
        // etc. are the immutable, server-generated identity of the Document and are
        // intentionally NOT present here - they cannot be changed through a normal
        // metadata update.
        //
        // RelatedDocumentId belongs to that same immutable set: it is the revision
        // chain link, written once by ReviseAsync. It used to be accepted here, which
        // meant any caller that omitted it silently detached a revision from the one
        // it superseded.
        //
        // File is absent for the same reason, and it is the important one: the
        // revision suffix of a Document Number exists to control changes to document
        // content (QASWI-014-B, بازنگری). Accepting a replacement file here allowed
        // content to change while the revision stayed put - and because files are
        // named after the Document Number, a same-extension upload overwrote the
        // previous content irrecoverably. Replacing content is what ReviseAsync is
        // for; it writes the new file under a new Number and keeps the old one.
    }
}