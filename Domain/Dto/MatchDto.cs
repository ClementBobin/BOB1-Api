namespace Domain.Dto;

using Domain.Enums;

public record RoleSlotDto(
    OfficialRole Role,
    UserDto? AssignedUser);

public record MatchDto(
    Guid Id,
    DateTime DateUtc,
    DateTime? EmergencyDateUtc,
    int EmergencyPoints,
    DivisionDto Division,
    TeamDto HomeTeam,
    TeamDto AwayTeam,
    LocationDto Location,
    IEnumerable<RoleSlotDto> Slots,
    MatchSubscriptionStatus? CurrentUserStatus); // null when called without auth context

public record LocationDto(Guid Id, string Name, string Address);

public record CreateMatchRequest(
    DateTime DateUtc,
    DateTime? EmergencyDateUtc,
    int EmergencyPoints,
    Guid DivisionId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Guid LocationId);

public record SubscribeRequest(OfficialRole Role);
