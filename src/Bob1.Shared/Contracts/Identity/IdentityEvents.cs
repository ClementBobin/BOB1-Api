namespace Bob1.Shared.Contracts.Identity;

/// <summary>An account was disabled: its slot signups and rides leave holes for the organisers.</summary>
public sealed record UserDeactivated(Guid UserId) : DomainEvent;

/// <summary>The last member left a family and it was deleted: what only existed for that family (presence confirmations) goes too.</summary>
public sealed record FamilyDissolved(Guid FamilyId) : DomainEvent;
