using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Domain.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserService _userService;

        // Maps a stored file's extension to a Content-Type for Download. Static because
        // the mapping table is immutable and building it per request is wasteful.
        private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        // Roles permitted to create or modify documents. Declared once so the write
        // endpoints cannot drift apart from each other.
        //
        // DocumentManager is included deliberately: managing controlled documents is
        // the entire purpose of that role, and the client has always granted it the
        // "documents:manage" capability (see hseq-client/src/auth/roles.ts). While
        // these endpoints accepted Admin only, the mismatch was invisible until a
        // DocumentManager had filled in a whole form and pressed save, and got a 403.
        //
        // Read endpoints below stay open to every authenticated user.
        private const string ManageDocumentRoles = "Admin,DocumentManager";

        public DocumentController(IDocumentService documentService, IUnitOfWork unitOfWork, IUserService userService)
        {
            _documentService = documentService;
            _unitOfWork = unitOfWork;
            _userService = userService;
        }

        [Authorize(Roles = ManageDocumentRoles)]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add([FromForm] CreateDocumentRequestModel req)
        {
            var pcode = _userService.GetPCodeFromToken(Request);
            await _documentService.AddAsync(req, pcode);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = ManageDocumentRoles)]
        [HttpPost]
        [Route("Update")]
        public async Task<IActionResult> Update([FromForm] UpdateDocumentRequestModel request)
        {
            await _documentService.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        // Distinct from Update on purpose: Update edits the metadata of one revision,
        // Revise issues the next revision as a new Document and retires this one.
        [Authorize(Roles = ManageDocumentRoles)]
        [HttpPost]
        [Route("Revise")]
        public async Task<IActionResult> Revise([FromForm] ReviseDocumentRequestModel request)
        {
            var pcode = _userService.GetPCodeFromToken(Request);
            await _documentService.ReviseAsync(request, pcode);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = ManageDocumentRoles)]
        [HttpPost]
        [Route("Delete")]
        public async Task<IActionResult> Delete([FromForm] Guid documentId)
        {
            await _documentService.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("Get")]
        public async Task<DocumentDto> Get(Guid documentId)
        {
            var res = await _documentService.GetByIdAsync(documentId);
            return res;
        }

        // Streams the file of one specific document. Reading is not restricted to
        // Admin - it matches Get/paged, which any authenticated user may call.
        // Superseded revisions are downloadable through the same route: each keeps
        // its own file, so this is how revision history is actually read.
        [HttpGet]
        [Route("Download")]
        public async Task<IActionResult> Download(Guid documentId, [FromQuery] bool asAttachment = false)
        {
            var file = await _documentService.GetFileAsync(documentId);

            if (!_contentTypeProvider.TryGetContentType(file.FileName, out var contentType))
                contentType = "application/octet-stream";

            // Default to inline so the browser previews PDFs/images in a tab instead of
            // forcing a save; asAttachment=true is the explicit "download" path.
            if (asAttachment)
                return File(file.Content, contentType, file.FileName);

            Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
            return File(file.Content, contentType);
        }

        // The full revision chain the given document belongs to, oldest revision first.
        [HttpGet]
        [Route("revisions")]
        public async Task<IActionResult> GetRevisionHistory([FromQuery] Guid documentId)
        {
            var result = await _documentService.GetRevisionHistoryAsync(documentId);
            return Ok(result);
        }

        // در فایل HSEQ.API/Controllers/DocumentController.cs
        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool includeInactiveItems = false)
        {
            var result = await _documentService.GetAllPaginationAsync(pageNumber, pageSize, includeInactiveItems);
            return Ok(result);
        }

    }
}
