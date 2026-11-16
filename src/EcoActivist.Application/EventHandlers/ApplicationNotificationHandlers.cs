using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.EventHandlers;

/// <summary>
/// Domain event handlers are dispatched from <c>SaveChangesAsync</c> before the changes are flushed,
/// so notifications added here are persisted atomically with the state change that produced them.
/// </summary>
public sealed class ApplicationNotificationHandlers :
    INotificationHandler<DomainEventNotification<ApplicationConfirmedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ApplicationRejectedDomainEvent>>,
    INotificationHandler<DomainEventNotification<ApplicationExpiredDomainEvent>>
{
    private readonly IApplicationDbContext _db;

    public ApplicationNotificationHandlers(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DomainEventNotification<ApplicationConfirmedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var title = await GetEventTitleAsync(e.EventId, ct);
        _db.Notifications.Add(Notification.Create(
            e.UserId,
            "Заявка подтверждена",
            $"Ваша заявка на мероприятие «{title}» подтверждена. QR-код для отметки явки доступен в личном кабинете.",
            e.OccurredOn));
    }

    public async Task Handle(DomainEventNotification<ApplicationRejectedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var title = await GetEventTitleAsync(e.EventId, ct);
        _db.Notifications.Add(Notification.Create(
            e.UserId,
            "Заявка отклонена",
            $"Ваша заявка на мероприятие «{title}» отклонена. Причина: {e.Reason}",
            e.OccurredOn));
    }

    public async Task Handle(DomainEventNotification<ApplicationExpiredDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var title = await GetEventTitleAsync(e.EventId, ct);
        _db.Notifications.Add(Notification.Create(
            e.UserId,
            "Заявка не рассмотрена",
            $"Организатор не рассмотрел вашу заявку на мероприятие «{title}» до начала приёма участников, заявка аннулирована.",
            e.OccurredOn));
    }

    private async Task<string> GetEventTitleAsync(Guid eventId, CancellationToken ct) =>
        await _db.Events.AsNoTracking().Where(ev => ev.Id == eventId).Select(ev => ev.Title).FirstOrDefaultAsync(ct)
        ?? "мероприятие";
}
