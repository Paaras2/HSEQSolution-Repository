using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IDocumentRelationService
    {
        // همه‌ی ارتباط‌های یک مدرک، بدون توجه به اینکه مدرک در کدام سمت ردیف است.
        Task<List<DocumentRelationDto>> GetForDocumentAsync(Guid documentId);

        Task AddAsync(CreateDocumentRelationRequestModel request, string pcode);

        Task RemoveAsync(Guid relationId);

        // انتقال ارتباط‌های یک مدرک به مدرک دیگر. توسط DocumentService.ReviseAsync
        // فراخوانی می‌شود تا ارتباط‌ها همراه زنجیره‌ی بازنگری جلو بیایند.
        Task TransferRelationsAsync(Guid fromDocumentId, Guid toDocumentId);
    }
}
