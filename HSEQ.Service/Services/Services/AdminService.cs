using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;

namespace HSEQ.Service.Services.Services
{
    // مدیریت نقش کاربران. جدول Admins تنها جایی است که نقش ماندگار نگه داشته می‌شود؛
    // نبودِ ردیف یعنی کاربر «فقط مشاهده» است، پس تنزل نقش = حذف ردیف.
    //
    // قواعد دسترسی اینجا اعمال می‌شوند نه در کنترلر، چون هر سه‌شان به وضعیت دیتابیس
    // نیاز دارند (نقش فعلیِ هدف، و تعداد مدیران سیستم باقی‌مانده).
    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _adminRepository;

        public AdminService(IAdminRepository adminRepository)
        {
            _adminRepository = adminRepository;
        }

        public async Task<bool> IsAdminAsync(int Pcode)
        {
            var admin = await _adminRepository.GetByPcodeAsync(Pcode);
            return admin is not null && admin.Role == AppRole.Admin;
        }

        public async Task<AppRole?> GetRoleAsync(int pcode)
        {
            var user = await _adminRepository.GetByPcodeAsync(pcode);
            return user?.Role;
        }

        public async Task<List<AppUserDto>> GetUsersAsync()
        {
            var users = await _adminRepository.GetAllAsync();
            return users.Select(u => new AppUserDto
            {
                Key = u.Key,
                Pcode = u.Pcode,
                Role = u.Role,
                CreatedTime = u.CreatedTime,
                ModifiedDate = u.ModifiedDate
            }).ToList();
        }

        public async Task SetUserRoleAsync(SetUserRoleRequestModel request, int actingPcode, AppRole actingRole)
        {
            // نقشِ تعریف‌نشده (مثلاً ۰ یا یک عدد دلخواه) صریحاً رد می‌شود. بدون این، مقدارِ
            // خارج از enum بی‌سروصدا در دیتابیس می‌نشست و معنایش بعداً نامشخص می‌شد.
            if (!Enum.IsDefined(typeof(AppRole), request.Role))
                throw new InvalidUserRoleException();

            if (request.Pcode <= 0)
                throw new InvalidUserPcodeException();

            if (request.Pcode == actingPcode)
                throw new CannotChangeOwnRoleException();

            var existing = await _adminRepository.GetByPcodeAsync(request.Pcode);

            // مدیر اسناد فقط می‌تواند مدیر اسناد تعریف کند. اگر نقشِ خواسته‌شده یا نقشِ
            // فعلیِ هدف «مدیر سیستم» باشد، فقط مدیر سیستم اجازه دارد - وگرنه یک مدیر
            // اسناد می‌توانست حسابی را ارتقا دهد و از همان راه خودش را بالا بکشد.
            var touchesAdminRole = request.Role == AppRole.Admin || existing?.Role == AppRole.Admin;
            if (touchesAdminRole && actingRole != AppRole.Admin)
                throw new InsufficientRoleToGrantException();

            if (existing is null)
            {
                await _adminRepository.AddAsync(new Admin
                {
                    Pcode = request.Pcode,
                    Role = request.Role,
                    IsActive = true,
                    CreatedTime = DateTime.Now
                });
                return;
            }

            if (existing.Role == request.Role)
                return;

            // تنزل آخرین مدیر سیستم، سامانه را بدون کسی که بتواند نقش بدهد رها می‌کند.
            if (existing.Role == AppRole.Admin && request.Role != AppRole.Admin)
                await EnsureNotLastAdminAsync();

            existing.Role = request.Role;
            existing.ModifiedDate = DateTime.Now;
            await _adminRepository.UpdateAsync(existing);
        }

        public async Task RemoveUserRoleAsync(int pcode, int actingPcode, AppRole actingRole)
        {
            if (pcode == actingPcode)
                throw new CannotChangeOwnRoleException();

            var existing = await _adminRepository.GetByPcodeAsync(pcode);
            if (existing is null)
                return;

            if (existing.Role == AppRole.Admin)
            {
                if (actingRole != AppRole.Admin)
                    throw new InsufficientRoleToGrantException();

                await EnsureNotLastAdminAsync();
            }

            await _adminRepository.DeleteAsync(existing);
        }

        // حذف فیزیکی ردیف است نه غیرفعال‌سازی: نقش یک وضعیت جاری است، نه رکورد
        // کنترل‌شده‌ای که تاریخچه‌اش ارزش نگه‌داری داشته باشد.
        private async Task EnsureNotLastAdminAsync()
        {
            var users = await _adminRepository.GetAllAsync();
            var adminCount = users.Count(u => u.Role == AppRole.Admin);
            if (adminCount <= 1)
                throw new LastAdminCannotBeRemovedException();
        }
    }
}
