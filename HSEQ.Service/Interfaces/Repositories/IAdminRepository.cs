using HSEQ.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Service.Interfaces.Repositories
{
    public interface IAdminRepository
    {
        //یک متد غیرهمزمان تعریف شده که یک Pcode می‌گیرد
        //می‌رود از دیتابیس یک Admin پیدا می‌کند
        //اگر پیدا شد همان آبجکت Admin را برمی‌گرداند، اگر پیدا نشد null برمی‌گرداند.»
        Task<Admin?> GetByPcodeAsync(int Pcode);
       
    }
}
