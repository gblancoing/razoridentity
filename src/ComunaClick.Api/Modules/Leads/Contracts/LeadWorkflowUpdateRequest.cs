namespace ComunaClick.Api.Modules.Leads.Contracts;

public sealed record LeadWorkflowUpdateRequest(
    string? Status,
    string? Priority,
    string? Owner,
    DateTimeOffset? NextFollowUpAt,
    string? InternalNote,
    string? OutcomeReason
);
