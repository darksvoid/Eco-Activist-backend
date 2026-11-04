using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using NetTopologySuite.Geometries;

namespace EcoActivist.Domain.Entities;

public class Event : AggregateRoot
{
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DateTimeOffset Date { get; private set; }
    public Point Location { get; private set; } = null!;
    public int Capacity { get; private set; }
    public EventStatus Status { get; private set; }
    public Guid OrganizerId { get; private set; }

    private Event() { }

    public Event(
        Guid id,
        string title,
        string description,
        DateTimeOffset date,
        Point location,
        int capacity,
        Guid organizerId) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(location);
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        Title = title;
        Description = description;
        Date = date;
        Location = location;
        Capacity = capacity;
        OrganizerId = organizerId;
        Status = EventStatus.Draft;
    }

    public static Event Create(
        string title,
        string description,
        DateTimeOffset date,
        Point location,
        int capacity,
        Guid organizerId)
    {
        return new Event(Guid.NewGuid(), title, description, date, location, capacity, organizerId);
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot publish event in status '{Status}'. Only Draft events can be published.");
        }

        Status = EventStatus.Published;
        AddDomainEvent(new EventPublishedDomainEvent(Id, DateTimeOffset.UtcNow));
    }

    public void Cancel(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == EventStatus.Cancelled || Status == EventStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot cancel event in status '{Status}'.");
        }

        Status = EventStatus.Cancelled;
        AddDomainEvent(new EventCancelledDomainEvent(Id, reason, DateTimeOffset.UtcNow));
    }

    public void Complete()
    {
        if (Status != EventStatus.Published)
        {
            throw new InvalidOperationException($"Cannot complete event in status '{Status}'.");
        }

        Status = EventStatus.Completed;
    }
}
