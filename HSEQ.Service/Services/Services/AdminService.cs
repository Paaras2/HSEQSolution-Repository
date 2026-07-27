using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Service.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace HSEQ.Service.Services.Services
{
    public class AdminService : IAdminService
    {


        //تعریف یک فیلد با نام _adminRepository;
        private readonly IAdminRepository _adminRepository;
      

        public AdminService(IAdminRepository adminRepository)
        {
            _adminRepository = adminRepository;
        }



        public async Task<bool> IsAdminAsync(int Pcode)
        {
            var admin = await _adminRepository.GetByPcodeAsync(Pcode);
            return admin is not null;
        }

     
    }
}
