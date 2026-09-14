using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IJwtService
    {
        Task<string> GenerateJwtToken(string username, string givenName = null, string familyName = null);
        Task<string> GenerateDevToken(string username, string role, string givenName = null, string familyName = null);
    }
}
