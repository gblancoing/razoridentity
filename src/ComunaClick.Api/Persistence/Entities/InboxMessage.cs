namespace ComunaClick.Api.Persistence.Entities;

public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string SenderRole { get; set; } = "customer";
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public InboxThread Thread { get; set; } = null!;
}
