using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;

namespace EcoActivist.Domain.Entities;

public class User : AggregateRoot
{
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public UserRole Role { get; private set; }

    private User() { }

    public User(Guid id, string email, string phone, UserRole role) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        Email = email;
        Phone = phone;
        Role = role;
    }

    public static User Create(string email, string phone, UserRole role)
    {
        return new User(Guid.NewGuid(), email, phone, role);
    }

    public void UpdateContactInfo(string email, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        Email = email;
        Phone = phone;
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
    }
}
