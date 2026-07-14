namespace Domain.Dto;

public record PenaltyDto(
    Guid Id,
    string Reason,
    int Points,
    bool KickedOut,
    DateTime? AcknowledgedAt,
    DateTime CreatedAt,
    Guid UserId,
    Guid? MatchId);

public record CreatePenaltyRequest(
    Guid UserId,
    Guid? MatchId,
    string Reason,
    int Points,
    bool KickedOut);
