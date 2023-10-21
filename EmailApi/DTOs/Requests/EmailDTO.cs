using System.Collections.Generic;

namespace EmailApi
{
    public class EmailDTO
    {
        public EmailDTO()
        {
            Smtp = new SmtpDTO();
            Message = new MessageDTO();
        }

        public string File { get; set; }
        public SmtpDTO Smtp { get; set; }
        public MessageDTO Message { get; set; }

        public class SmtpDTO
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public bool EnableSSL { get; set; }
        }

        public class MessageDTO
        {
            public MessageDTO()
            {
                To = new HashSet<MailAddressDTO>();
                Cc = new HashSet<MailAddressDTO>();
                Bcc = new HashSet<MailAddressDTO>();
            }

            public MailAddressDTO From { get; set; }
            public ICollection<MailAddressDTO> To { get; set; }
            public ICollection<MailAddressDTO> Cc { get; set; }
            public ICollection<MailAddressDTO> Bcc { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public bool IsBodyHtml { get; set; }

            public class MailAddressDTO
            {
                public string Address { get; set; }
                public string Name { get; set; }
            }
        }
    }
}
