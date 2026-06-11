namespace ComunaClick.Api.Persistence.Entities;

public sealed class ServiceImageSnapshot
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
