namespace Domain.Dto;

public record SeasonPointDto(
    Guid Id,
    int SeasonId,
    Guid UserId,
    Guid? MatchId,
    int Points,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SeasonPointSummaryDto(
    int SeasonId,
    Guid UserId,
    int TotalPoints,
    IEnumerable<SeasonPointDto> Entries);

public record SeasonPointRankingDto(
    int Rank,
    Guid UserId,
    string FullName,
    int TotalPoints);