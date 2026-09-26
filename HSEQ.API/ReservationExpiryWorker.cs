using HSEQ.Service.Interfaces.Services;

namespace HSEQ.API
{
    // جاروبِ دوره‌ای که رزروهای بی‌فایلِ منقضی‌شده (بیش از ۷۲ ساعت) را به Expired می‌برد.
    // کار یک UPDATEِ مجموعه‌ایِ idempotent است، پس اجرای همزمانش هم بی‌خطر است.
    //
    // ⚠ روی IIS با میزبانی in-process، اگر App Pool به‌خاطر بی‌کاری خاموش شود این worker هم
    // می‌خوابد تا اولین درخواستِ بعدی. برای انقضای مطمئن، Idle Time-out را صفر و Start Mode
    // را AlwaysRunning بگذارید، یا این کار را به‌جای worker با یک SQL Agent Job اجرا کنید.
    public sealed class ReservationExpiryWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationExpiryWorker> _logger;

        public ReservationExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationExpiryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var reservations = scope.ServiceProvider.GetRequiredService<IDocumentReservationService>();
                    var expired = await reservations.ExpireStaleReservationsAsync(stoppingToken);
                    if (expired > 0)
                        _logger.LogInformation("{Count} رزروِ منقضی‌شده به Expired تغییر یافت.", expired);
                }
                catch (OperationCanceledException)
                {
                    break; // خاموش‌شدنِ عادیِ برنامه
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "جاروبِ انقضای رزرو شکست خورد؛ در نوبتِ بعد دوباره تلاش می‌شود.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
