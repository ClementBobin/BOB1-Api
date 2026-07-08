namespace Domain.Entities;

using Domain.Enums;

/// <summary>
/// An official role slot on a match. Stored as an owned collection on <see cref="Match"/>.
/// </summary>
public class RoleSlot
{
    public int Id { get; set; } // shadow PK for owned collection table

    public OfficialRole Role { get; set; }

    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    // Back-reference (set by EF)
    public Guid MatchId { get; set; }
}
