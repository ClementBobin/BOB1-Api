namespace Domain.Dto;

using Domain.Enums;

public record RoleSlotDto(
    OfficialRole Role,
    UserDto? AssignedUser);

public record MatchDto(
    Guid Id,
    DateTime DateUtc,
    DateTime? EmergencyDateUtc,
    DivisionDto Division,
    TeamDto HomeTeam,
    TeamDto AwayTeam,
    LocationDto Location,
    IEnumerable<RoleSlotDto> Slots,
    MatchSubscriptionStatus? CurrentUserStatus); // null when called without auth context

public record MinMatchDto(
    Guid Id,
    DateTime DateUtc,
    DivisionDto Division,
    LocationDto Location,
    bool areSlotsAvailable,
    MatchSubscriptionStatus? CurrentUserStatus);


public record CreateMatchRequest(
    DateTime DateUtc,
    DateTime? EmergencyDateUtc,
    Guid DivisionId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Guid LocationId);

public record SubscribeRequest(OfficialRole Role);
