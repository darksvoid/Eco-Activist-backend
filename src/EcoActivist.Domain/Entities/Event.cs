using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using EcoActivist.Domain.Exceptions;
using NetTopologySuite.Geometries;

namespace EcoActivist.Domain.Entities;

public class Event : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int AddressMaxLength = 500;
    public const int ParticipationConditionsMaxLength = 2000;

    public static readonly TimeSpan ApplicationReviewCutoff = TimeSpan.FromHours(24);
    public static readonly TimeSpan AttendanceMarkingGracePeriod = TimeSpan.FromHours(48);

    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public DateTimeOffset Date { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public Point Location { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string? ParticipationConditions { get; private set; }
    public int Capacity { get; private set; }
    public EventStatus Status { get; private set; }
    public Guid OrganizerId { get; private set; }

    private Event() { }

    public Event(
        Guid id,
        string title,
        string description,
        Guid categoryId,
        DateTimeOffset date,
        DateTimeOffset endDate,
        Point location,
        string address,
        string? participationConditions,
        int capacity,
        Guid organizerId) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(location);
        if (categoryId == Guid.Empty)
            throw new ArgumentException("CategoryId cannot be empty.", nameof(categoryId));
        if (organizerId == Guid.Empty)
            throw new ArgumentException("OrganizerId cannot be empty.", nameof(organizerId));
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
        if (location.SRID != GeoLocation.Wgs84Srid)
            throw new ArgumentException($"Location SRID must be {GeoLocation.Wgs84Srid}.", nameof(location));

        EnsureValidSchedule(date, endDate);

        Title = title.Trim();
        Description = description.Trim();
        CategoryId = categoryId;
        Date = date.ToUniversalTime();
        EndDate = endDate.ToUniversalTime();
        Location = location;
        Address = address.Trim();
        ParticipationConditions = string.IsNullOrWhiteSpace(participationConditions) ? null : participationConditions.Trim();
        Capacity = capacity;
        OrganizerId = organizerId;
        Status = EventStatus.Draft;
    }

    public static Event Create(
        string title,
        string description,
        Guid categoryId,
        DateTimeOffset date,
        DateTimeOffset endDate,
        Point location,
        string address,
        string? participationConditions,
        int capacity,
        Guid organizerId)
    {
        return new Event(Guid.NewGuid(), title, description, categoryId, date, endDate, location, address,
            participationConditions, capacity, organizerId);
    }

    public static void EnsureValidSchedule(DateTimeOffset date, DateTimeOffset endDate)
    {
        if (endDate <= date)
            throw new ArgumentException("EndDate must be later than the event start Date.", nameof(endDate));
    }

    public DateTimeOffset ApplicationDeadline => Date - ApplicationReviewCutoff;

    public DateTimeOffset AttendanceMarkingDeadline => EndDate + AttendanceMarkingGracePeriod;

    public bool IsOwnedBy(Guid organizerId) => OrganizerId == organizerId;

    public bool HasStarted(DateTimeOffset now) => now >= Date;

    public bool IsAcceptingApplications(DateTimeOffset now) =>
        Status == EventStatus.Published && now < ApplicationDeadline;

    public bool CanApplicationBeCancelled(DateTimeOffset now) => now < Date;

    public bool CanApplicationsBeReviewed(DateTimeOffset now) =>
        Status == EventStatus.Published && now < Date;

    public bool IsAttendanceWindowOpen(DateTimeOffset now) =>
        Status == EventStatus.Published && now >= Date && now <= AttendanceMarkingDeadline;

    public bool IsAttendanceWindowClosed(DateTimeOffset now) => now > AttendanceMarkingDeadline;

    public bool CanBeCancelled(DateTimeOffset now) =>
        Status is EventStatus.Draft or EventStatus.Published && now < Date;

    public void EnsureApplicationCanBeCancelled(DateTimeOffset now)
    {
        if (!CanApplicationBeCancelled(now))
            throw new DomainException("Отмена заявки возможна только до начала мероприятия.");
    }

    public void EnsureAttendanceWindowOpen(DateTimeOffset now)
    {
        if (Status != EventStatus.Published)
            throw new DomainException($"Отметка явки недоступна для мероприятия в статусе '{Status}'.");
        if (!IsAttendanceWindowOpen(now))
            throw new DomainException("Отметка явки доступна с момента начала мероприятия и в течение 48 часов после его окончания.");
    }

    public void Reschedule(DateTimeOffset date, DateTimeOffset endDate)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed)
            throw new DomainException($"Cannot reschedule event in status '{Status}'.");

        EnsureValidSchedule(date, endDate);
        Date = date.ToUniversalTime();
        EndDate = endDate.ToUniversalTime();
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
        {
            throw new DomainException($"Cannot publish event in status '{Status}'. Only Draft events can be published.");
        }

        Status = EventStatus.Published;
        AddDomainEvent(new EventPublishedDomainEvent(Id, DateTimeOffset.UtcNow));
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!CanBeCancelled(now))
        {
            throw new DomainException($"Cannot cancel event in status '{Status}' or after it has started.");
        }

        Status = EventStatus.Cancelled;
        AddDomainEvent(new EventCancelledDomainEvent(Id, reason, now));
    }

    public void Complete()
    {
        if (Status != EventStatus.Published)
        {
            throw new DomainException($"Cannot complete event in status '{Status}'.");
        }

        Status = EventStatus.Completed;
    }
}
