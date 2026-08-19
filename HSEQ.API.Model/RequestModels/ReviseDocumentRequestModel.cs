using Microsoft.AspNetCore.Http;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // Issues a new revision of an existing Document. The result is a new Document
    // row, not an edit of the one identified by Key - see IDocumentService.ReviseAsync.
    public class ReviseDocumentRequestModel
    {
        // The revision being superseded. Must be the newest revision in its chain.
        public Guid Key { get; set; }

        // Optional: a revision may retitle the document. Left empty, the superseded
        // revision's Name carries over.
        public string? Name { get; set; }

        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }

        // Required in practice, but declared nullable so the missing-file case is
        // rejected by RevisionFileRequiredException rather than by [ApiController]
        // model validation - the latter returns a ProblemDetails payload the client's
        // error parser doesn't understand, and an untranslated message.
        public IFormFile? File { get; set; }

        // Number, revision, serial and the whole organizational classification are
        // NOT accepted here: a revision inherits its classification verbatim from the
        // document it supersedes, and its Number is computed server-side by
        // IDocumentNumberGeneratorService.GenerateNextRevisionAsync.
    }
}
