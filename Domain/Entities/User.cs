namespace Domain.Entities;

using Domain.Enums;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? BiometricToken { get; set; }

    // Navigation - using the mapping entity instead of the enum directly
    public ICollection<UserRoleMapping> Roles { get; set; } = [];

    // Other Navigation collections...
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Penalty> Penalties { get; set; } = [];
    public ICollection<AppNotification> Notifications { get; set; } = [];
}