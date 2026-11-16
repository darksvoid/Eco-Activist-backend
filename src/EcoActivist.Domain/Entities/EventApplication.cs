using System.Security.Cryptography;
using System.Text;
using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using EcoActivist.Domain.Exceptions;

namespace EcoActivist.Domain.Entities;

public class EventApplication : AggregateRoot
{
    public const int QrCodeTokenMaxLength = 512;
    public const int RejectionReasonMaxLength = 1000;

    public Guid UserId { get; private set; }
    public Guid EventId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? QrCodeToken { get; private set; }
    public DateTimeOffset? QrCodeGeneratedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }

    private EventApplication() { }

    public EventApplication(Guid id, Guid userId, Guid eventId, DateTimeOffset createdAt) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        UserId = userId;
        EventId = eventId;
        Status = ApplicationStatus.Pending;
        CreatedAt = createdAt;

        AddDomainEvent(new ApplicationSubmittedDomainEvent(Id, EventId, UserId, CreatedAt));
    }

    public static EventApplication Create(Guid userId, Guid eventId, DateTimeOffset createdAt)
    {
        return new EventApplication(Guid.NewGuid(), userId, eventId, createdAt);
    }

    public static bool IsActiveStatus(ApplicationStatus status) =>
        status is ApplicationStatus.Pending or ApplicationStatus.Confirmed;

    public bool IsActive => IsActiveStatus(Status);

    public void Confirm(string qrToken, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qrToken);
        if (qrToken.Length > QrCodeTokenMaxLength)
            throw new ArgumentException("QR token is too long.", nameof(qrToken));

        if (Status != ApplicationStatus.Pending)
        {
            throw new DomainException($"Cannot confirm application in status '{Status}'. Status must be Pending.");
        }

        Status = ApplicationStatus.Confirmed;
        QrCodeToken = qrToken;
        QrCodeGeneratedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new ApplicationConfirmedDomainEvent(Id, EventId, UserId, now));
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!IsActive)
        {
            throw new DomainException($"Cannot reject application in status '{Status}'. Status must be Pending or Confirmed.");
        }

        var trimmed = reason.Trim();
        Status = ApplicationStatus.Rejected;
        RejectionReason = trimmed.Length > RejectionReasonMaxLength ? trimmed[..RejectionReasonMaxLength] : trimmed;
        RevokeQrCode();
        UpdatedAt = now;
        AddDomainEvent(new ApplicationRejectedDomainEvent(Id, EventId, UserId, RejectionReason, now));
    }

    public void Cancel(DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new DomainException($"Cannot cancel application in status '{Status}'. Only Pending or Confirmed applications can be cancelled.");
        }

        Status = ApplicationStatus.Cancelled;
        RevokeQrCode();
        UpdatedAt = now;
        AddDomainEvent(new ApplicationCancelledDomainEvent(Id, EventId, UserId, now));
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Pending)
        {
            throw new DomainException($"Cannot expire application in status '{Status}'. Status must be Pending.");
        }

        Status = ApplicationStatus.Expired;
        UpdatedAt = now;
        AddDomainEvent(new ApplicationExpiredDomainEvent(Id, EventId, UserId, now));
    }

    public void MarkEventCancelled(DateTimeOffset now)
    {
        if (!IsActive)
            return;

        Status = ApplicationStatus.EventCancelled;
        RevokeQrCode();
        UpdatedAt = now;
    }

    public void MarkReminderSent(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Confirmed)
            throw new DomainException("Напоминание может быть отправлено только по подтверждённой заявке.");

        ReminderSentAt = now;
    }

    public bool MatchesQrToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(QrCodeToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(QrCodeToken),
            Encoding.UTF8.GetBytes(token));
    }

    private void RevokeQrCode()
    {
        QrCodeToken = null;
        QrCodeGeneratedAt = null;
    }
}
