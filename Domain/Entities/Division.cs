namespace Domain.Entities;

public class Division
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "U17", "U15"

    // Navigation
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Match> Matches { get; set; } = [];
}
