using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(string documentNumber, IFormFile file);
        string? GetSignFile(string nationalCode);
    }
}
