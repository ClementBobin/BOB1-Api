namespace Domain.Dto;

public record TeamDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    DivisionDto Division);

public record CreateTeamRequest(
    string Name,
    Guid DivisionId,
    string? LogoUrl);
