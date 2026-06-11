using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly OrderNotificationOptions _options;

    public SmtpEmailSender(IOptions<OrderNotificationOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Smtp.Host) && !string.IsNullOrWhiteSpace(_options.Smtp.From);

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SMTP no está configurado (Host/From vacíos).");
        }

        using var smtp = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            smtp.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
        }

        using var mail = new MailMessage(_options.Smtp.From, to)
        {
            Subject = subject,
            Body = body
        };

        await smtp.SendMailAsync(mail, cancellationToken);
    }
}
