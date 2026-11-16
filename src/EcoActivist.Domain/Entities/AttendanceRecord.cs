using EcoActivist.Domain.Common;
using EcoActivist.Domain.Enums;

namespace EcoActivist.Domain.Entities;

public class AttendanceRecord : AggregateRoot
{
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public DateTimeOffset MarkedAt { get; private set; }
    public Guid MarkedByOrganizerId { get; private set; }

    private AttendanceRecord() { }

    public AttendanceRecord(Guid id, Guid eventId, Guid userId, AttendanceStatus status, DateTimeOffset markedAt, Guid markedByOrganizerId)
        : base(id)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (markedByOrganizerId == Guid.Empty)
            throw new ArgumentException("MarkedByOrganizerId cannot be empty.", nameof(markedByOrganizerId));
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown attendance status.");

        EventId = eventId;
        UserId = userId;
        Status = status;
        MarkedAt = markedAt;
        MarkedByOrganizerId = markedByOrganizerId;
    }

    public static AttendanceRecord MarkAttended(Guid eventId, Guid userId, Guid organizerId, DateTimeOffset markedAt) =>
        new(Guid.NewGuid(), eventId, userId, AttendanceStatus.Attended, markedAt, organizerId);

    public static AttendanceRecord MarkAbsent(Guid eventId, Guid userId, Guid organizerId, DateTimeOffset markedAt) =>
        new(Guid.NewGuid(), eventId, userId, AttendanceStatus.Absent, markedAt, organizerId);
}
