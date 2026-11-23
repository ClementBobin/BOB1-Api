namespace Bob1.Modules.Club.Domain;

// Plain reference data: no business rules, so no ceremony.
internal sealed class Division
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";   // "U13 M"
    public string Category { get; set; } = ""; // U11, U13, U15, U17, U18, Senior
    public string Gender { get; set; } = "";   // Masculin, Féminin, Mixte
    public bool IsActive { get; set; } = true;
}

internal sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid DivisionId { get; set; }
    public string? CoachName { get; set; }
    public Guid? CoachUserId { get; set; }  // Foreign key to User if coach is a staff member
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? TrainingVenueId { get; set; }  // Foreign key to Venue
}

internal sealed class Venue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
}

/// <summary>A club season (1 Sept → 31 Aug). Created automatically; "current" is derived from today's date.</summary>
internal sealed class ClubSeason
{
    public string Label { get; set; } = default!;   // "2026-2027"
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }

    public static ClubSeason For(string label) => new() { Label = label, StartsOn = Season.StartsOn(label), EndsOn = Season.EndsOn(label) };
}
