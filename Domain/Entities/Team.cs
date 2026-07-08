namespace Domain.Entities;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    // FK
    public Guid DivisionId { get; set; }
    public Division Division { get; set; } = null!;
}
