using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.MailService
{
    public interface IMailSender
    {
        Task<bool> SendMailAsync(MailContent content);
    }
}
