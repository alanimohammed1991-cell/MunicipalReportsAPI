using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MunicipalReportsAPI.Models;

namespace MunicipalReportsAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailConfirmationAsync(ApplicationUser user, string confirmationLink)
        {
            var subject = "تأكيد البريد الإلكتروني - نظام البلاغات البلدية";
            var body = $@"
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; direction: rtl; text-align: right; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
                        .button {{ background-color: #007bff; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; color: #6c757d; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>مرحباً بك في نظام البلاغات البلدية!</h2>
                        </div>
                        <div class='content'>
                            <p>مرحباً {user.FullName ?? user.Email}،</p>
                            <p>شكراً لك على التسجيل في نظام البلاغات البلدية. لإكمال تسجيلك، يرجى تأكيد عنوان بريدك الإلكتروني بالنقر على الرابط أدناه:</p>
                            <p style='text-align: center;'>
                                <a href='{confirmationLink}' class='button'>تأكيد البريد الإلكتروني</a>
                            </p>
                            <p>إذا لم يعمل الزر، يمكنك نسخ ولصق هذا الرابط في متصفحك:</p>
                            <p style='word-break: break-all; background-color: #e9ecef; padding: 10px; border-radius: 5px;'>{confirmationLink}</p>
                            <p><strong>ملاحظة:</strong> هذا الرابط صالح لمدة 24 ساعة لأسباب أمنية.</p>
                            <p>إذا لم تقم بإنشاء هذا الحساب، يرجى تجاهل هذا البريد الإلكتروني.</p>
                            <div class='footer'>
                                <p>مع أطيب التحيات،<br>فريق نظام البلاغات البلدية</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";

            return await SendEmailAsync(user.Email, subject, body);
        }

        public async Task<bool> SendPasswordResetAsync(string email, string resetLink)
        {
            var subject = "إعادة تعيين كلمة المرور - نظام البلاغات البلدية";
            var body = $@"
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; direction: rtl; text-align: right; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
                        .button {{ background-color: #dc3545; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
                        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; color: #856404; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; color: #6c757d; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>🔒 طلب إعادة تعيين كلمة المرور</h2>
                        </div>
                        <div class='content'>
                            <p>مرحباً،</p>
                            <p>لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بك في نظام البلاغات البلدية. انقر على الرابط أدناه لإعادة تعيين كلمة المرور:</p>
                            <p style='text-align: center;'>
                                <a href='{resetLink}' class='button'>إعادة تعيين كلمة المرور</a>
                            </p>
                            <p>إذا لم يعمل الزر، يمكنك نسخ ولصق هذا الرابط في متصفحك:</p>
                            <p style='word-break: break-all; background-color: #e9ecef; padding: 10px; border-radius: 5px;'>{resetLink}</p>
                            <div class='warning'>
                                <p><strong>⚠️ تنبيه أمني:</strong></p>
                                <ul>
                                    <li>هذا الرابط صالح لمدة ساعة واحدة فقط لأسباب أمنية</li>
                                    <li>إذا لم تطلب إعادة تعيين كلمة المرور، يرجى تجاهل هذا البريد الإلكتروني</li>
                                    <li>لا تشارك هذا الرابط مع أي شخص آخر</li>
                                </ul>
                            </div>
                            <div class='footer'>
                                <p>مع أطيب التحيات،<br>فريق نظام البلاغات البلدية</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendWelcomeEmailAsync(ApplicationUser user)
        {
            var subject = "مرحباً بك في نظام البلاغات البلدية!";
            var body = $@"
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; direction: rtl; text-align: right; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
                        .features {{ background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-right: 4px solid #28a745; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; color: #6c757d; }}
                        ul {{ text-align: right; }}
                        li {{ margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>🎉 مرحباً بك في نظام البلاغات البلدية!</h2>
                        </div>
                        <div class='content'>
                            <p>مرحباً {user.FullName ?? user.Email}،</p>
                            <p>تم تأكيد بريدك الإلكتروني بنجاح وأصبح حسابك نشطاً الآن!</p>
                            <div class='features'>
                                <p><strong>يمكنك الآن:</strong></p>
                                <ul>
                                    <li>📝 تقديم بلاغات حول المشاكل البلدية</li>
                                    <li>📊 تتبع حالة بلاغاتك</li>
                                    <li>👤 تحديث معلومات الملف الشخصي</li>
                                    <li>🗺️ عرض البلاغات في منطقتك</li>
                                </ul>
                            </div>
                            <p><strong>شكراً لك على مساعدتنا في جعل مجتمعنا أفضل!</strong></p>
                            <p>نحن نقدر مشاركتك في تحسين الخدمات البلدية وحل المشاكل في منطقتك.</p>
                            <div class='footer'>
                                <p>مع أطيب التحيات،<br>فريق نظام البلاغات البلدية</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";

            return await SendEmailAsync(user.Email, subject, body);
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                var smtpServer = emailSettings["SmtpServer"];
                var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
                var senderEmail = emailSettings["SenderEmail"];
                var senderPassword = emailSettings["SenderPassword"];
                var senderName = emailSettings["SenderName"];

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, senderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
                return false;
            }
        }
    }
}