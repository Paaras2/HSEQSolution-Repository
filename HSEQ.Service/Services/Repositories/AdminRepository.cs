using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Repositories
{
    public class AdminRepository : Repository<Admin>, IAdminRepository
    {
        private readonly ApplicationDbContext _context;

        public AdminRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Admin?> GetByPcodeAsync(int Pcode)
        {
            return await _context.Set<Admin>()
                .FirstOrDefaultAsync(x => x.Pcode == Pcode);
        }

        // مدیران سیستم اول، بعد مدیران اسناد؛ داخل هر گروه به ترتیب کد پرسنلی.
        public async Task<List<Admin>> GetAllAsync()
        {
            return await _context.Set<Admin>()
                .OrderByDescending(a => a.Role)
                .ThenBy(a => a.Pcode)
                .ToListAsync();
        }
    }
}
