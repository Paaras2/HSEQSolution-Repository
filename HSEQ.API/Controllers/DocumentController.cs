using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Domain.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        public DocumentController(IDocumentService documentService, IUnitOfWork unitOfWork, IUserService userService)
        {
            _documentService = documentService;
            _unitOfWork = unitOfWork;
            _userService = userService;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add([FromForm] CreateDocumentRequestModel req)
        {
            var pcode = _userService.GetPCodeFromToken(Request);
            await _documentService.AddAsync(req, pcode);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Route("Update")]
        public async Task<IActionResult> Update([FromForm] UpdateDocumentRequestModel request)
        {
            await _documentService.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Admin")]
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

        // در فایل HSEQ.API/Controllers/DocumentController.cs
        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _documentService.GetAllPaginationAsync(pageNumber, pageSize);
            return Ok(result);
        }

    }
}
