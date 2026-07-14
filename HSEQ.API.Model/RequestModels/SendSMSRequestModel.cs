using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.API.Model.RequestModels
{
    public class SendSMSRequestModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string To { get; set; }
        public string Text { get; set; }
        public string Domain { get; set; }
        public string From { get; set; }
        public string ApiUsername { get; set; }
        public string APIPassword { get; set; }
    }
}
