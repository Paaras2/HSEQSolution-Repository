using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IJwtService
    {
        Task<string> GenerateJwtToken(string username);
        Task<string> GenerateDevToken(string username, string role);
    }
}