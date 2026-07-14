namespace Domain.Dto;

using Domain.Enums;

public record RoleSlotDto(
    OfficialRole Role,
    UserPublicDto? AssignedUser);


public record MatchDto(
    Guid Id,
    DateTime DateUtc,
    DateTime? EmergencyDateUtc,
    DivisionDto Division,
    TeamDto HomeTeam,
    TeamDto AwayTeam,
    LocationDto Location,
    IEnumerable<RoleSlotDto> Slots,
    MatchSubscriptionStatus? CurrentUserStatus);

public record CreateMatchRequest(
    DateTime DateUtc,
    Guid DivisionId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Guid LocationId);

public record SubscribeRequest(OfficialRole Role);
