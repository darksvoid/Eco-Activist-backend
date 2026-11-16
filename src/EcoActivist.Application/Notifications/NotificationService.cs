using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Models;
using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Notifications;

public sealed class NotificationListQuery : PageRequest
{
    public bool UnreadOnly { get; init; }
}

public sealed record NotificationDto(Guid Id, string Title, string Message, DateTimeOffset CreatedAt, bool IsRead);

public sealed class NotificationService
{
    private readonly IApplicationDbContext _db;

    public NotificationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<NotificationDto>> GetMyAsync(Guid userId, NotificationListQuery query, CancellationToken ct)
    {
        var notifications = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
            notifications = notifications.Where(n => !n.IsRead);

        var total = await notifications.CountAsync(ct);
        var items = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.CreatedAt, n.IsRead))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, query.Page, query.PageSize, total);
    }

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
                           ?? throw NotFoundException.For<Notification>(notificationId);

        if (notification.IsRead)
            return;

        notification.MarkAsRead();
        await _db.SaveChangesAsync(ct);
    }
}
