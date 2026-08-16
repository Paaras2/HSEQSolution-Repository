using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace HSEQ.Service.Services.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IFileService _fileService;
        private readonly IDocumentNumberGeneratorService _documentNumberGenerator;

        public DocumentService(
            IDocumentRepository documentRepository,
            IFileService fileService,
            IDocumentNumberGeneratorService documentNumberGenerator)
        {
            _documentRepository = documentRepository;
            _fileService = fileService;
            _documentNumberGenerator = documentNumberGenerator;
        }
        //Add
        public async Task AddAsync(CreateDocumentRequestModel request, string pcode)
        {
            var generated = await _documentNumberGenerator.GenerateAsync(
                request.ProjectId,
                request.OrganizationalManagementId,
                request.OrganizationalActivityId,
                request.DocumentTypeId);

            var fileName = await _fileService.SaveFileAsync(generated.Number, request.File);
            var document = new Domain.Entities.Document
            {
                CreatedByPCode = Convert.ToInt32(pcode),
                CurrentReviewDate = request.CurrentReviewDate,
                FileName = fileName,
                FormerReviewDate = request.FormerReviewDate,
                IsActive = true,
                Number = generated.Number,
                SerialNumber = generated.SerialNumber,
                LastVersion = generated.LastVersion,
                ContentRevision = generated.ContentRevision,
                ProjectId = request.ProjectId,
                OrganizationalManagementId = request.OrganizationalManagementId,
                OrganizationalActivityId = request.OrganizationalActivityId,
                DocumentTypeId = request.DocumentTypeId,
                Name = request.Name,
                RelatedDocumentId = request.RelatedDocumentId
            };

            await _documentRepository.AddAsync(document);
        }
        //Update
        public async Task UpdateAsync(UpdateDocumentRequestModel request)
        {
            var document = await _documentRepository.GetByIdAsync(request.Key);
            if (document == null)
                throw new DocumentNotFoundException();

            if (request.File != null)
            {
                document.FileName = await _fileService.SaveFileAsync(document.Number, request.File);
            }

            document.Name = request.Name;
            document.CurrentReviewDate = request.CurrentReviewDate;
            document.RelatedDocumentId = request.RelatedDocumentId;
            document.FormerReviewDate = request.FormerReviewDate;

            await _documentRepository.UpdateAsync(document);
        }
        //Delete
        public async Task DeleteAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            document.IsActive = false;
            await _documentRepository.UpdateAsync(document);
        }
       // GetAll
        public async Task<List<DocumentDto>> GetAllAsync(bool includeDeactiveItems = true)
        {
            throw new Exception();
        }
        //GetById
        public async Task<DocumentDto> GetByIdAsync(Guid id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                throw new DocumentNotFoundException();

            return new DocumentDto
            {
                CreatedTime = document.CreatedTime,
                CreatedByPCode = document.CreatedByPCode,
                CurrentReviewDate = document.CurrentReviewDate,
                FileName = document.FileName,
                FormerReviewDate = document.FormerReviewDate,
                IsActive = document.IsActive,
                Key = document.Key,
                LastVersion = document.LastVersion,
                ContentRevision = document.ContentRevision,
                SerialNumber = document.SerialNumber,
                ModifiedDate = document.ModifiedDate,
                Name = document.Name,
                Number = document.Number,
                RelatedDocumentId = document.RelatedDocumentId,
                ProjectId = document.ProjectId,
                OrganizationalManagementId = document.OrganizationalManagementId,
                OrganizationalActivityId = document.OrganizationalActivityId,
                DocumentTypeId = document.DocumentTypeId,
            };
        }
        //Pagination
        public async Task<PagedResult> GetAllPaginationAsync(
            int pageNumber,
            int pageSize,
            bool includeDeactiveItems = true)
        {
            if (pageNumber < 1)
                pageNumber = 1;

            if (pageSize < 1)
                pageSize = 10;

            var query = _documentRepository.GetAllAsQueryable();

            if (!includeDeactiveItems)
            {
                query = query.Where(x => x.IsActive);
            }

            var totalCount = await query.CountAsync();

            var documents = await query
                .OrderBy(x => x.Key)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult
            {
                Items = documents.Select(MapToDto).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static DocumentDto MapToDto(Domain.Entities.Document document)
        {
            return new DocumentDto
            {
                Key = document.Key,
                IsActive = document.IsActive,
                CreatedTime = document.CreatedTime,
                ModifiedDate = document.ModifiedDate,
                Number = document.Number,
                Name = document.Name,
                FormerReviewDate = document.FormerReviewDate,
                CurrentReviewDate = document.CurrentReviewDate,
                LastVersion = document.LastVersion,
                ContentRevision = document.ContentRevision,
                SerialNumber = document.SerialNumber,
                RelatedDocumentId = document.RelatedDocumentId,
                RelatedDocumentNumber = null,
                FileName = document.FileName,
                ProjectId = document.ProjectId,
                OrganizationalManagementId = document.OrganizationalManagementId,
                OrganizationalActivityId = document.OrganizationalActivityId,
                DocumentTypeId = document.DocumentTypeId,
                File = null,
                CreatedByPCode = document.CreatedByPCode
            };
        }
    }
}