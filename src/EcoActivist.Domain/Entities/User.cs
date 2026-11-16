using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Exceptions;

namespace EcoActivist.Domain.Entities;

public class User : AggregateRoot
{
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 50;

    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public int EcoPointsBalance { get; private set; }
    public OrganizerStatus? Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    public User(Guid id, string email, string phone, string passwordHash, UserRole role, DateTimeOffset createdAt) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Email = NormalizeEmail(email);
        Phone = phone.Trim();
        PasswordHash = passwordHash;
        Role = role;
        Status = role == UserRole.Organizer ? OrganizerStatus.Pending : null;
        EcoPointsBalance = 0;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string phone, string passwordHash, UserRole role, DateTimeOffset createdAt)
    {
        return new User(Guid.NewGuid(), email, phone, passwordHash, role, createdAt);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool IsApprovedOrganizer => Role == UserRole.Organizer && Status == OrganizerStatus.Approved;

    public void UpdateContactInfo(string email, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        Email = NormalizeEmail(email);
        Phone = phone.Trim();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void ChangeRole(UserRole newRole)
    {
        if (Role == newRole)
            return;

        Role = newRole;
        Status = newRole == UserRole.Organizer ? OrganizerStatus.Pending : null;
    }

    public void AddEcoPoints(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "EcoPoints amount must be positive.");

        EcoPointsBalance = checked(EcoPointsBalance + amount);
    }

    public void ApproveOrganizer() => ChangeOrganizerStatus(OrganizerStatus.Approved);

    public void RejectOrganizer() => ChangeOrganizerStatus(OrganizerStatus.Rejected);

    public void SendOrganizerToRevision() => ChangeOrganizerStatus(OrganizerStatus.OnRevision);

    private void ChangeOrganizerStatus(OrganizerStatus status)
    {
        if (Role != UserRole.Organizer)
            throw new DomainException("Статус организатора применим только к пользователям с ролью Organizer.");

        Status = status;
    }
}
