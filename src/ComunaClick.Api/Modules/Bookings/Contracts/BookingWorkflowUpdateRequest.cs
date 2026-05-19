namespace ComunaClick.Api.Modules.Bookings.Contracts;

public sealed record BookingWorkflowUpdateRequest(
    string? Status,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? InternalNote,
    string? OutcomeReason
);
