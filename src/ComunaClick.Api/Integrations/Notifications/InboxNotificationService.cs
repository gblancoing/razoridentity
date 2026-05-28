using System.Net;
using System.Net.Mail;
using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

public sealed class InboxNotificationService : IInboxNotificationService
{
    private readonly OrderNotificationOptions _options;
    private readonly CoreDbContext _db;
    private readonly ILogger<InboxNotificationService> _logger;

    public InboxNotificationService(
        IOptions<OrderNotificationOptions> options,
        CoreDbContext db,
        ILogger<InboxNotificationService> logger)
    {
        _options = options.Value;
        _db = db;
        _logger = logger;
    }

    public async Task NotifyNewInboxMessageAsync(
        Guid threadId,
        Guid messageId,
        string senderRole,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.EnableEmail)
        {
            return;
        }

        var thread = await _db.InboxThreads.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Partner)
            .Include(x => x.Professional)
            .FirstOrDefaultAsync(x => x.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return;
        }

        var message = await _db.InboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == messageId && x.ThreadId == threadId, cancellationToken);

        if (message is null || string.IsNullOrWhiteSpace(message.Body))
        {
            return;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var subjectLine = string.IsNullOrWhiteSpace(thread.Subject) ? "Consulta en ComunaClic" : thread.Subject.Trim();
        var preview = Truncate(message.Body, 400);
        var counterpartyLabel = thread.Partner?.Name ?? thread.Professional?.Name ?? "ComunaClic";

        if (string.Equals(senderRole, "customer", StringComparison.OrdinalIgnoreCase))
        {
            var to = thread.Partner?.Email;
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var inboxUrl = $"{baseUrl}/partner/messages?threadId={thread.Id}";
            var body = string.Join(Environment.NewLine, new[]
            {
                "Hola,",
                string.Empty,
                "Recibiste un mensaje nuevo en ComunaClic.",
                $"Asunto: {subjectLine}",
                $"De: {thread.Customer?.FullName ?? thread.Customer?.Email ?? "Cliente"}",
                string.Empty,
                "Mensaje:",
                preview,
                string.Empty,
                "Respondé desde tu buzón:",
                inboxUrl,
                string.Empty,
                "— ComunaClic"
            });

            await TrySendEmailAsync(to.Trim(), $"Nuevo mensaje: {subjectLine}", body, cancellationToken);
            return;
        }

        if (string.Equals(senderRole, "business", StringComparison.OrdinalIgnoreCase))
        {
            var to = thread.Customer?.Email;
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var inboxUrl = $"{baseUrl}/account/messages?threadId={thread.Id}";
            var customerName = thread.Customer?.FullName;
            var body = string.Join(Environment.NewLine, new[]
            {
                "Hola" + (string.IsNullOrWhiteSpace(customerName) ? "" : $" {customerName.Trim()}") + ",",
                string.Empty,
                $"{counterpartyLabel} te respondió en ComunaClic.",
                $"Asunto: {subjectLine}",
                string.Empty,
                "Mensaje:",
                preview,
                string.Empty,
                "Ver conversación:",
                inboxUrl,
                string.Empty,
                "— ComunaClic"
            });

            await TrySendEmailAsync(to.Trim(), $"Respuesta de {counterpartyLabel}", body, cancellationToken);
        }
    }

    private async Task TrySendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Smtp.Host) || string.IsNullOrWhiteSpace(_options.Smtp.From))
        {
            return;
        }

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to send inbox notification email to {Email}.", to);
        }
    }

    private static string Truncate(string value, int max)
    {
        var trimmed = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..(max - 3)] + "...";
    }
}
