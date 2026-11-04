using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;

namespace EcoActivist.Domain.Entities;

public class EventApplication : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid EventId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private EventApplication() { }

    public EventApplication(Guid id, Guid userId, Guid eventId) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        UserId = userId;
        EventId = eventId;
        Status = ApplicationStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ApplicationSubmittedDomainEvent(Id, EventId, UserId, CreatedAt));
    }

    public static EventApplication Create(Guid userId, Guid eventId)
    {
        return new EventApplication(Guid.NewGuid(), userId, eventId);
    }

    public void Confirm()
    {
        if (Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot confirm application in status '{Status}'. Status must be Pending.");
        }

        Status = ApplicationStatus.Confirmed;
        AddDomainEvent(new ApplicationConfirmedDomainEvent(Id, EventId, UserId, DateTimeOffset.UtcNow));
    }

    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot reject application in status '{Status}'. Status must be Pending.");
        }

        Status = ApplicationStatus.Rejected;
        AddDomainEvent(new ApplicationRejectedDomainEvent(Id, EventId, UserId, reason, DateTimeOffset.UtcNow));
    }

    public void Cancel()
    {
        if (Status != ApplicationStatus.Pending && Status != ApplicationStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot cancel application in status '{Status}'. Only Pending or Confirmed applications can be cancelled.");
        }

        Status = ApplicationStatus.Cancelled;
        AddDomainEvent(new ApplicationCancelledDomainEvent(Id, EventId, UserId, DateTimeOffset.UtcNow));
    }
}
