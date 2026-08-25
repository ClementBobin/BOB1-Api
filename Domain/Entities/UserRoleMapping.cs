namespace Domain.Entities;

using Domain.Enums;

public class UserRoleMapping
{
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
}