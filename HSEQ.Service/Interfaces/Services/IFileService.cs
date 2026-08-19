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

        // Opens a previously saved document file for reading. Returns null when the
        // stored name no longer resolves to a file inside the upload folder, which the
        // caller should surface as "file missing" rather than as a server error.
        // The caller owns the returned stream.
        Stream? OpenDocumentFile(string fileName);

        string? GetSignFile(string nationalCode);
    }
}
