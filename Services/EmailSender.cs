using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Mail;

namespace DesignerStore.Services;

public class EmailSender : IEmailSender<IdentityUser>
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration config, IWebHostEnvironment env, ILogger<EmailSender> logger)
    {
        _config = config;
        _env    = env;
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
        => SendAsync(email, "Підтвердження пошти — ELARIUM", confirmationLink,
            $@"<div style='font-family:sans-serif;max-width:520px;margin:auto;padding:40px 32px;'>
                <h2 style='letter-spacing:4px;font-weight:300;margin:0 0 8px;'>ELARIUM</h2>
                <hr style='border:none;border-top:1px solid #ddd;margin:0 0 24px;'>
                <p style='margin:0 0 16px;'>Підтвердіть вашу електронну пошту, перейшовши за посиланням:</p>
                <a href='{confirmationLink}'
                   style='display:inline-block;padding:12px 32px;background:#111;color:#fff;
                          text-decoration:none;letter-spacing:1px;font-size:0.85rem;'>
                    ПІДТВЕРДИТИ ПОШТУ
                </a>
                <p style='color:#999;font-size:0.78rem;margin-top:24px;'>
                    Якщо ви не реєструвались — проігноруйте цей лист.
                </p>
            </div>");

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
        => SendAsync(email, "Відновлення паролю — ELARIUM", resetLink,
            $@"<div style='font-family:sans-serif;max-width:520px;margin:auto;padding:40px 32px;'>
                <h2 style='letter-spacing:4px;font-weight:300;margin:0 0 8px;'>ELARIUM</h2>
                <hr style='border:none;border-top:1px solid #ddd;margin:0 0 24px;'>
                <p style='margin:0 0 8px;'>Ми отримали запит на відновлення паролю для вашого акаунту.</p>
                <p style='margin:0 0 24px;'>Натисніть кнопку нижче, щоб встановити новий пароль:</p>
                <a href='{resetLink}'
                   style='display:inline-block;padding:12px 32px;background:#111;color:#fff;
                          text-decoration:none;letter-spacing:1px;font-size:0.85rem;'>
                    СКИНУТИ ПАРОЛЬ
                </a>
                <p style='color:#999;font-size:0.78rem;margin-top:24px;'>
                    Посилання дійсне 24 години.<br>
                    Якщо ви не надсилали цей запит — просто проігноруйте цей лист.
                </p>
            </div>");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
        => SendAsync(email, "Код відновлення паролю — ELARIUM", resetCode,
            $@"<div style='font-family:sans-serif;max-width:520px;margin:auto;padding:40px 32px;'>
                <h2 style='letter-spacing:4px;font-weight:300;margin:0 0 8px;'>ELARIUM</h2>
                <hr style='border:none;border-top:1px solid #ddd;margin:0 0 24px;'>
                <p>Ваш код відновлення паролю:</p>
                <p style='font-size:1.6rem;font-weight:300;letter-spacing:6px;margin:16px 0;'>{resetCode}</p>
            </div>");

    // внутрішній метод відправки 
    private async Task SendAsync(string to, string subject, string devLink, string htmlBody)
    {
        // У режимі розробки — завжди виводимо посилання в консоль
        if (_env.IsDevelopment())
        {
            _logger.LogWarning(
                "📧 [DEV] Email до {To}\n   Тема: {Subject}\n   Посилання: {Link}",
                to, subject, devLink);
        }

        var cfg = _config.GetSection("Email");
        var userName = cfg["UserName"];
        var password = cfg["Password"];

        if (string.IsNullOrWhiteSpace(userName)
            || userName.Contains("your@")
            || string.IsNullOrWhiteSpace(password)
            || password == "your-app-password")
        {
            _logger.LogWarning(
                "⚠️  SMTP не налаштовано. Заповніть секцію \"Email\" в appsettings.json " +
                "(UserName / Password / From). Лист НЕ відправлено.");
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From       = new MailAddress(cfg["From"]!, cfg["FromName"] ?? "ELARIUM"),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true,
            };
            message.To.Add(to);

            using var smtp = new SmtpClient(cfg["Host"] ?? "smtp.gmail.com",
                                             int.Parse(cfg["Port"] ?? "587"))
            {
                EnableSsl   = bool.Parse(cfg["EnableSsl"] ?? "true"),
                Credentials = new NetworkCredential(userName, password),
            };

            await smtp.SendMailAsync(message);
            _logger.LogInformation("✅ Email надіслано до {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка відправки email до {To}: {Message}", to, ex.Message);
        }
    }
}
