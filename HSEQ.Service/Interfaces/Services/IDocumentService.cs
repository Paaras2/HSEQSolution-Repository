using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IDocumentService
    {
        Task AddAsync(CreateDocumentRequestModel request);
        Task UpdateAsync(UpdateDocumentRequestModel request);
        Task DeleteAsync(Guid documenttId);
        Task<List<DocumentDto>> GetAllAsync(bool includeDeactiveItems = true);
        Task<DocumentDto> GetByIdAsync(Guid id);
    }
}
