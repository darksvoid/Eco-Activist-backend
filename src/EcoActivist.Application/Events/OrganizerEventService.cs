using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Events;

public sealed class OrganizerEventService
{
    private readonly IApplicationDbContext _db;
    private readonly EventCatalogService _catalog;
    private readonly TimeProvider _timeProvider;

    public OrganizerEventService(IApplicationDbContext db, EventCatalogService catalog, TimeProvider timeProvider)
    {
        _db = db;
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> CreateAsync(Guid organizerId, CreateEventRequest request, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new RequestValidationException(nameof(request.CategoryId), "Указанная категория не существует.");

        if (request.Date <= _timeProvider.GetUtcNow())
            throw new RequestValidationException(nameof(request.Date), "Дата начала мероприятия должна быть в будущем.");

        if (request.EndDate <= request.Date)
            throw new RequestValidationException(nameof(request.EndDate), "Дата окончания должна быть позже даты начала.");

        var ev = Event.Create(
            request.Title,
            request.Description,
            request.CategoryId,
            request.Date,
            request.EndDate,
            GeoLocation.CreatePoint(request.Latitude, request.Longitude),
            request.Address,
            request.ParticipationConditions,
            request.Capacity,
            organizerId);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync(ct);

        return ev.Id;
    }

    public async Task<EventDetailsDto> PublishAsync(Guid organizerId, Guid eventId, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        var ev = await _db.GetOwnedEventAsync(eventId, organizerId, ct);
        if (ev.Date <= _timeProvider.GetUtcNow())
            throw new ConflictException("Нельзя опубликовать мероприятие, дата начала которого уже наступила.");

        ev.Publish();
        await _db.SaveChangesAsync(ct);

        return await _catalog.GetEventAsync(eventId, ct);
    }

    public async Task CancelAsync(Guid organizerId, Guid eventId, string reason, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        await _db.ExecuteInTransactionAsync(async token =>
        {
            if (!await _db.LockForUpdateAsync<Event>(eventId, token))
                throw NotFoundException.For<Event>(eventId);

            var ev = await _db.GetOwnedEventAsync(eventId, organizerId, token);
            var now = _timeProvider.GetUtcNow();

            ev.Cancel(reason, now);

            var activeApplications = await _db.EventApplications
                .Where(a => a.EventId == eventId)
                .WhereActive()
                .ToListAsync(token);

            foreach (var application in activeApplications)
            {
                application.MarkEventCancelled(now);
                _db.Notifications.Add(Notification.Create(
                    application.UserId,
                    "Мероприятие отменено",
                    $"Мероприятие «{ev.Title}» отменено организатором. Причина: {reason.Trim()}",
                    now));
            }

            await _db.SaveChangesAsync(token);
            return true;
        }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<OrganizerEventDto>> GetMyEventsAsync(Guid organizerId, CancellationToken ct)
    {
        var applications = _db.EventApplications;

        var rows = await _db.Events.AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.Date)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Date,
                e.EndDate,
                e.Status,
                e.Capacity,
                Pending = applications.Count(a => a.EventId == e.Id && a.Status == ApplicationStatus.Pending),
                Confirmed = applications.Count(a => a.EventId == e.Id && a.Status == ApplicationStatus.Confirmed)
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new OrganizerEventDto(
                r.Id, r.Title, r.Date, r.EndDate, r.Status, r.Capacity, r.Pending, r.Confirmed,
                Math.Max(0, r.Capacity - r.Pending - r.Confirmed)))
            .ToList();
    }
}
