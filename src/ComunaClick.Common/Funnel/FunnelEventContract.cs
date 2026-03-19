namespace ComunaClick.Common.Funnel;

public sealed record FunnelEventRequest(
    string EventName,
    string Vertical,
    string EntityType,
    Guid? EntityId,
    Guid? TenantId,
    string Origin,
    string DeviceType,
    string CtaType,
    string Step,
    string Status,
    DateTimeOffset Timestamp,
    Dictionary<string, string?>? Metadata = null);

public sealed record FunnelEventAck(bool Accepted);
