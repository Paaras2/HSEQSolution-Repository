using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.API.Model.Dtos
{
    public class CheckCredentialDto
    {
        public int PCode { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Mobile { get; set; }
        public bool IsActive { get; set; }
        public bool IsFirstLogin { get; set; }
        public string NationalCode { get; set; }
        public string UserName { get; set; }
        public DateTime? LastModificationDate { get; set; }
    }
}
