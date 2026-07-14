namespace Domain.Dto;

using Domain.Enums;

public record PointRuleDto(
    Guid Id,
    OfficialRole Role,
    int PointsOnConfirmation1,
    int PointsOnConfirmation2,
    int PointsEmergency);

public record UpdatePointRuleRequest(
    int PointsOnConfirmation1,
    int PointsOnConfirmation2,
    int PointsEmergency);
