using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.API.Model.Dtos
{
    public class SendSMSResponseDto
    {
        public int Status { get; set; }
        public string Response { get; set; }
        public bool IsSucceeded { get; set; }
    }

    public class SmsApiResult
    {
        public SendSMSResponseDto Response { get; set; }
    }
}
