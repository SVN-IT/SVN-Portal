using MailKit.Security;
using Microsoft.AspNetCore.Http;
using MimeKit;
using System.Net.Mime;

namespace SVNShareLib
{
    public class MailService
    {
        public async Task<BODataProcessResult> SendMail(MailContentModel mailContent, MailSettingModel mailSetting)
        {
            BODataProcessResult result = new BODataProcessResult();
            var email = new MimeMessage();
            email.Sender = new MailboxAddress(mailSetting.DisplayName, mailSetting.Mail);
            email.From.Add(new MailboxAddress(mailSetting.DisplayName, mailSetting.Mail));
            //email.To.Add(MailboxAddress.Parse(mailContent.To));
            email.Subject = mailContent.Subject;

            //add danh sách địa chỉ email được gửi
            if (mailContent.To != null && mailContent.To.Count > 0)
            {
                InternetAddressList listEmailCc = new InternetAddressList();
                foreach (var item in mailContent.To)
                {
                    listEmailCc.Add(MailboxAddress.Parse(item));
                }
                email.To.AddRange(listEmailCc);
            }

            //add danh sách địa chỉ email được cc
            if (mailContent.Cc != null && mailContent.Cc.Count > 0)
            {
                InternetAddressList listEmailCc = new InternetAddressList();
                foreach (var item in mailContent.Cc)
                {
                    listEmailCc.Add(MailboxAddress.Parse(item));
                }
                email.Cc.AddRange(listEmailCc);
            }

            var builder = new BodyBuilder();
            builder.HtmlBody = mailContent.Body;
            #region attach file
            byte[] fileBytes;
            if (mailContent.Files != null && mailContent.Files.Count > 0)
            {
                foreach (var item in mailContent.Files)
                {
                    var file = item;
                    if (file.Length > 0)
                    {
                        using (var ms = new MemoryStream())
                        {
                            file.CopyTo(ms);
                            fileBytes = ms.ToArray();
                        }
                        builder.Attachments.Add(file.FileName, fileBytes, MimeKit.ContentType.Parse(MediaTypeNames.Application.Pdf));
                    }
                }


            }
            #endregion
            email.Body = builder.ToMessageBody();
            // dùng SmtpClient của MailKit
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                smtp.Connect(mailSetting.Host, mailSetting.Port, SecureSocketOptions.None);
                smtp.Authenticate(mailSetting.Mail, mailSetting.Password);

                await smtp.SendAsync(email);
                smtp.Disconnect(true);
                result.OK = true;
                //logger.LogInformation("Send mail to " + mailContent.To);
            }
            catch (Exception ex)
            {
                result.OK = false;
                //result.Message = "Unable to authenticate the login credentials provided by the user";
                result.Message = ex.Message;
                // Gửi mail thất bại, nội dung email sẽ lưu vào thư mục mailssave
                System.IO.Directory.CreateDirectory("mailssave");
                var emailsavefile = string.Format(@"mailssave/{0}.eml", Guid.NewGuid());
                await email.WriteToAsync(emailsavefile);

                //logger.LogInformation("Lỗi gửi mail, lưu tại - " + emailsavefile);
                //logger.LogError(ex.Message);
            }
            smtp.Disconnect(true);
            return result;
        }
    }

    public class MailContentModel
    {
        public MailContentModel()
        {
            To = new List<string>();
            Cc = new List<string>();
        }
        public List<string> To { get; set; }              // Địa chỉ gửi đến
        public string Subject { get; set; }         // Chủ đề (tiêu đề email)
        public string Body { get; set; }            // Nội dung (hỗ trợ HTML) của email
        public List<IFormFile> Files { get; set; }
        public List<string> Cc { get; set; }
    }

    public class MailSettingModel
    {
        public MailSettingModel()
        {

        }
        public string Code { get; set; } //CompanyCode
        public string Mail { get; set; }
        public string DisplayName { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
    }
}
