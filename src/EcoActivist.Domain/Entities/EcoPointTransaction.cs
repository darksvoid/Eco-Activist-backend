using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Entities;

public class EcoPointTransaction : AggregateRoot
{
    public const int ReasonMaxLength = 500;

    public Guid UserId { get; private set; }
    public int Amount { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? AttendanceRecordId { get; private set; }

    private EcoPointTransaction() { }

    public EcoPointTransaction(Guid id, Guid userId, int amount, string reason, DateTimeOffset createdAt, Guid? attendanceRecordId)
        : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (amount == 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Transaction amount cannot be zero.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var trimmed = reason.Trim();
        UserId = userId;
        Amount = amount;
        Reason = trimmed.Length > ReasonMaxLength ? trimmed[..ReasonMaxLength] : trimmed;
        CreatedAt = createdAt;
        AttendanceRecordId = attendanceRecordId;
    }

    public static EcoPointTransaction ForAttendance(Guid userId, int amount, string reason, DateTimeOffset createdAt, Guid attendanceRecordId) =>
        new(Guid.NewGuid(), userId, amount, reason, createdAt, attendanceRecordId);
}
