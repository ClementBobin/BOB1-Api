namespace Domain.Entities;

using Domain.Enums;

public class PointRule
{
    public Guid Id { get; set; }
    public OfficialRole Role { get; set; }
    public int PointsOnJ15 { get; set; }
    public int PointsOnJ4 { get; set; }
    public int PointsEmergency { get; set; }
}
