namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

public sealed record PartnerChecklistItemResponse(
    string Key,
    string Label,
    bool IsComplete,
    string? Hint);

public sealed record PartnerActivationStatusResponse(
    Guid PartnerId,
    string PartnerType,
    bool IsVisible,
    bool CanPublish,
    int CompletionPercent,
    string Status,
    string StatusLabel,
    string NextStep,
    IReadOnlyList<PartnerChecklistItemResponse> Checklist);
