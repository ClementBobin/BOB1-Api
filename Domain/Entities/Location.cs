namespace Domain.Entities;

public class Location
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // Navigation
    public ICollection<Match> Matches { get; set; } = [];
}
