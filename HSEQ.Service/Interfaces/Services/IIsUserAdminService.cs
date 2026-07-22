using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IIsUserAdminService
    {
        Task<bool> IsAdminAsync(int Pcode);
    }
}
