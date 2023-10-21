using System.Collections.Generic;
using System.Net.Mail;

namespace SEEDUC.Email.Extensions
{
    public static class MailAddressCollectionExtension
    {
        public static void AddRange(this MailAddressCollection self, IEnumerable<MailAddress> mailAddresses)
        {
            foreach (var ma in mailAddresses)
                self.Add(ma);
        }

        public static void AddRange(this MailAddressCollection self, IEnumerable<(string Address, string DisplayName)> mailAddresses)
        {
            foreach (var ma in mailAddresses)
                self.Add(new MailAddress(ma.Address, ma.DisplayName));
        }
    }
}
