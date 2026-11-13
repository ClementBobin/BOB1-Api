namespace Bob1.Shared.Contracts.Identity;

/// <summary>An account was disabled: its slot signups and rides leave holes for the organisers.</summary>
public sealed record UserDeactivated(Guid UserId) : DomainEvent;
