namespace Domain.Dto;

public record CreatePenaltyRequest(
    Guid UserId,
    Guid? MatchId,
    string Reason,
    int Points,
    bool KickedOut);
