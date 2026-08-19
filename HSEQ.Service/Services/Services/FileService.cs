using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Services
{
    public class FileService : IFileService
    {
        private readonly string _uploadPath = AppSettingFactory.AppSetting.UploadPath;

        public async Task<string> SaveFileAsync(string documentNumber, IFormFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            var folderPath = _uploadPath;

            // Defensive: UploadPath is an environment-specific absolute path (see
            // appsettings.json) that isn't guaranteed to already exist on a given
            // machine/deployment - creating it here avoids an unhandled
            // DirectoryNotFoundException turning into a bare 500 on first upload.
            Directory.CreateDirectory(folderPath);

            // Get file extension from uploaded file, keep it (including the dot)
            var extension = Path.GetExtension(file.FileName);


            var fileName = documentNumber + extension;

            var fullPath = Path.Combine(folderPath, fileName);



            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return fileName;
        }

        public Stream? OpenDocumentFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var root = Path.GetFullPath(_uploadPath);
            if (!root.EndsWith(Path.DirectorySeparatorChar))
                root += Path.DirectorySeparatorChar;

            // FileName is a value this service generated, but it still travels through
            // the database before coming back here, so it is treated as untrusted:
            // GetFileName() drops any directory part and the root check rejects
            // anything that would resolve outside the upload folder.
            var fullPath = Path.GetFullPath(Path.Combine(root, Path.GetFileName(fileName)));

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(fullPath))
                return null;

            // FileShare.Read so a download never blocks a concurrent revision upload.
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }


        public string? GetFileAsBase64(string fileType, string nationalCode)
        {
            //if (string.IsNullOrWhiteSpace(fileType))
            //    throw new ArgumentException("File type must be provided.", nameof(fileType));
            //if (string.IsNullOrWhiteSpace(nationalCode))
            //    throw new ArgumentException("National code must be provided.", nameof(nationalCode));

            //var folderPath = GetUploadFolderPath(fileType);
            //var safeFileName = SanitizeFileName(nationalCode);

            //// Search for file with any extension
            //var filePath = Directory
            //    .GetFiles(folderPath, safeFileName + ".*")
            //    .FirstOrDefault();

            ////if (filePath == null || !File.Exists(filePath))
            ////    throw new FileNotFoundException("The requested file was not found.", safeFileName);

            //if (!File.Exists(filePath))
            //    return null;

            //byte[] bytes = File.ReadAllBytes(filePath);
            //string base64File = Convert.ToBase64String(bytes);

            //return base64File;
            return "sadasd";

        }


        public string? GetSignFile(string nationalCode)
        {
            var file = GetFileAsBase64("Sign", nationalCode);
            return file;
        }
    }
}