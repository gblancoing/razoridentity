namespace ComunaClick.Api.Persistence.Entities;

public sealed class SiteContentSetting
{
    public Guid Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
