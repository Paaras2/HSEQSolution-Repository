using HSEQ.API.Model.RequestModels;
using HSEQ.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    // جریانِ «رزرو کد»: اول کد گرفته می‌شود (بدون فایل)، بعداً فایل ضمیمه می‌شود.
    // تولیدِ سریال و یکتاییِ شماره همان مسیرِ امنِ موجود است (IDocumentNumberGeneratorService
    // + ایندکسِ یکتای Documents.Number)؛ این سرویس فقط لایه‌ی چرخه‌ی حیات را اضافه می‌کند.
    public interface IDocumentReservationService
    {
        // یک ردیفِ Reserved می‌سازد و برمی‌گرداند (Number/ReservationExpiresAtUtc پُر شده،
        // پیش از SaveChanges). فراخوان (کنترلر) با IUnitOfWork ذخیره می‌کند.
        Task<Document> ReserveAsync(ReserveDocumentRequestModel request, string pcode);

        // فایل را به یک مدرکِ Reserved ضمیمه و آن را Published می‌کند. اگر مدرک Reserved
        // نباشد یا مهلتش گذشته باشد، خطای وضعیت می‌دهد.
        Task AttachFileAsync(AttachDocumentFileRequestModel request);

        // رزروهای بی‌فایلِ منقضی‌شده را به Expired می‌برد. یک UPDATEِ مجموعه‌ایِ idempotent؛
        // تعداد ردیف‌های تغییرکرده را برمی‌گرداند. از worker دوره‌ای صدا زده می‌شود.
        Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default);
    }
}
