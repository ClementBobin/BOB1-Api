namespace Domain.Dto;

using Domain.Enums;

public record PointRuleDto(
    Guid Id,
    OfficialRole Role,
    int PointsOnJ15,
    int PointsOnJ4);

public record UpdatePointRuleRequest(
    int PointsOnJ15,
    int PointsOnJ4);