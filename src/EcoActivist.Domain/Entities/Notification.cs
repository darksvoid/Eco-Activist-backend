using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Entities;

public class Notification : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 2000;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRead { get; private set; }

    private Notification() { }

    public Notification(Guid id, Guid userId, string title, string message, DateTimeOffset createdAt) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        UserId = userId;
        Title = Truncate(title.Trim(), TitleMaxLength);
        Message = Truncate(message.Trim(), MessageMaxLength);
        CreatedAt = createdAt;
        IsRead = false;
    }

    public static Notification Create(Guid userId, string title, string message, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), userId, title, message, createdAt);

    public void MarkAsRead() => IsRead = true;

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
