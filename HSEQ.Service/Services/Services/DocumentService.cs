using Azure.Core;
using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IFileService _fileService;
        //change
        public DocumentService(IDocumentRepository documentRepository, IFileService fileService)
        {
            _documentRepository = documentRepository;
            _fileService = fileService;
        }


        //11111111111111111111111
        public async Task AddAsync(CreateDocumentRequestModel request)
        {
            var fileName = await _fileService.SaveFileAsync(request.Number, request.File);
            Document document = new Document
            {
                CreatedByPCode = 2292,
                CurrentReviewDate = request.CurrentReviewDate,
                FileName = fileName,
                FormerReviewDate = request.FormerReviewDate,
                IsActive = true,
                LastVersion = request.LastVersion,
                UnitId = request.UnitId,
                Name = request.Name,
                Number = request.Number,
                RelatedDocumentId = request.RelatedDocumentId
            };

            await _documentRepository.AddAsync(document);
        }
        //222222222222222222222222
        public async Task DeleteAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            document.IsActive = false;
            await _documentRepository.UpdateAsync(document);
        }
        //3333333333333333333333333
        public Task<List<DocumentDto>> GetAllAsync(bool includeDeactiveItems = true)
        {
            throw new NotImplementedException();
        }
        //4444444444444444444444444
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
                ModifiedDate = document.ModifiedDate,
                Name = document.Name,
                Number = document.Number,
                RelatedDocumentId = document.RelatedDocumentId,
                UnitId = document.UnitId,
                // UnitTitle = document.Unit.Title
            };
        }
        //5555555555555555555555555555555555555555
        public async Task UpdateAsync(UpdateDocumentRequestModel request)
        {
            var document = await _documentRepository.GetByIdAsync(request.Key);
            if (document == null)
                throw new DocumentNotFoundException();

            string fileName = string.Empty;
            if (request.File != null)
            {
                fileName = await _fileService.SaveFileAsync(request.Number, request.File);
            }

            document.UnitId = request.UnitId;
            document.Name = request.Name;
            document.Number = request.Number;
            document.LastVersion = request.LastVersion;
            document.CurrentReviewDate = request.CurrentReviewDate;
            document.RelatedDocumentId = request.RelatedDocumentId;
            document.FormerReviewDate = request.FormerReviewDate;
            document.FileName = fileName;

            await _documentRepository.UpdateAsync(document);

        }
    }
}
