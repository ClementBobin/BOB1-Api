namespace Domain.Dto;

using Domain.Enums;

public record RoleSlotDto(
    OfficialRole Role,
    UserDto? AssignedUser);

public record MatchDto(
    Guid Id,
    DateTime DateUtc,
    DivisionDto Division,
    TeamDto HomeTeam,
    TeamDto AwayTeam,
    LocationDto Location,
    IEnumerable<RoleSlotDto> Slots,
    MatchSubscriptionStatus? CurrentUserStatus);

public record MinMatchDto(
    Guid Id,
    DateTime DateUtc,
    DivisionDto Division,
    LocationDto Location,
    bool areSlotsAvailable,
    MatchSubscriptionStatus? CurrentUserStatus);

public record CreateMatchRequest(
    DateTime DateUtc,
    Guid DivisionId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Guid LocationId);

public record SubscribeRequest(OfficialRole Role);