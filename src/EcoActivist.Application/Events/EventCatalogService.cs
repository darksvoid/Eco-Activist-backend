using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Models;
using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace EcoActivist.Application.Events;

public sealed class EventCatalogService
{
    private readonly IApplicationDbContext _db;

    public EventCatalogService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<EventListItemDto>> GetPublishedEventsAsync(EventListQuery query, CancellationToken ct)
    {
        var userPoint = ResolveUserPoint(query);

        if (query.DateFrom is { } from && query.DateTo is { } to && from > to)
            throw new RequestValidationException(nameof(query.DateFrom), "Параметр dateFrom не может быть позже dateTo.");

        var events = _db.Events.AsNoTracking().Where(e => e.Status == EventStatus.Published);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            events = events.Where(e => e.Title.ToLower().Contains(term));
        }

        if (query.CategoryId is { } categoryId)
            events = events.Where(e => e.CategoryId == categoryId);

        if (query.DateFrom is { } from2)
        {
            var dateFrom = from2.ToUniversalTime();
            events = events.Where(e => e.Date >= dateFrom);
        }

        if (query.DateTo is { } to2)
        {
            var dateTo = to2.ToUniversalTime();
            events = events.Where(e => e.Date <= dateTo);
        }

        var totalCount = await events.CountAsync(ct);

        var rows = Project(events, userPoint);
        var descending = query.SortDirection == SortDirection.Desc;

        rows = query.SortBy switch
        {
            EventSortBy.AvailablePlaces => descending
                ? rows.OrderByDescending(r => r.Capacity - r.ActiveApplications).ThenBy(r => r.Date).ThenBy(r => r.Id)
                : rows.OrderBy(r => r.Capacity - r.ActiveApplications).ThenBy(r => r.Date).ThenBy(r => r.Id),
            EventSortBy.Distance => descending
                ? rows.OrderByDescending(r => r.DistanceMeters).ThenBy(r => r.Date).ThenBy(r => r.Id)
                : rows.OrderBy(r => r.DistanceMeters).ThenBy(r => r.Date).ThenBy(r => r.Id),
            _ => descending
                ? rows.OrderByDescending(r => r.Date).ThenBy(r => r.Id)
                : rows.OrderBy(r => r.Date).ThenBy(r => r.Id)
        };

        var page = await rows.Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);

        var items = page
            .Select(r => new EventListItemDto(
                r.Id,
                r.Title,
                r.CategoryId,
                r.CategoryName,
                r.EcoPointsReward,
                r.Date,
                r.EndDate,
                r.Address,
                GeoPointDto.From(r.Location),
                r.Capacity,
                Math.Max(0, r.Capacity - r.ActiveApplications),
                r.DistanceMeters))
            .ToList();

        return new PagedResult<EventListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<EventDetailsDto> GetEventAsync(Guid eventId, CancellationToken ct)
    {
        var applications = _db.EventApplications;
        var categories = _db.Categories;

        var details = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId && e.Status != EventStatus.Draft)
            .Select(e => new
            {
                Event = e,
                Category = categories.Where(c => c.Id == e.CategoryId).Select(c => new { c.Name, c.EcoPointsReward }).FirstOrDefault(),
                ActiveApplications = applications.Count(a => a.EventId == e.Id &&
                    (a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Confirmed))
            })
            .FirstOrDefaultAsync(ct);

        if (details is null)
            throw NotFoundException.For<Event>(eventId);

        var ev = details.Event;
        return new EventDetailsDto(
            ev.Id,
            ev.Title,
            ev.Description,
            ev.CategoryId,
            details.Category?.Name,
            details.Category?.EcoPointsReward ?? 0,
            ev.Date,
            ev.EndDate,
            ev.ApplicationDeadline,
            ev.Address,
            GeoPointDto.From(ev.Location),
            ev.ParticipationConditions,
            ev.Capacity,
            Math.Max(0, ev.Capacity - details.ActiveApplications),
            ev.Status,
            ev.OrganizerId);
    }

    private IQueryable<EventRow> Project(IQueryable<Event> events, Point? userPoint)
    {
        var applications = _db.EventApplications;
        var categories = _db.Categories;

        if (userPoint is null)
        {
            return events.Select(e => new EventRow
            {
                Id = e.Id,
                Title = e.Title,
                CategoryId = e.CategoryId,
                CategoryName = categories.Where(c => c.Id == e.CategoryId).Select(c => c.Name).FirstOrDefault(),
                EcoPointsReward = categories.Where(c => c.Id == e.CategoryId).Select(c => c.EcoPointsReward).FirstOrDefault(),
                Date = e.Date,
                EndDate = e.EndDate,
                Address = e.Address,
                Location = e.Location,
                Capacity = e.Capacity,
                ActiveApplications = applications.Count(a => a.EventId == e.Id &&
                    (a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Confirmed)),
                DistanceMeters = null
            });
        }

        return events.Select(e => new EventRow
        {
            Id = e.Id,
            Title = e.Title,
            CategoryId = e.CategoryId,
            CategoryName = categories.Where(c => c.Id == e.CategoryId).Select(c => c.Name).FirstOrDefault(),
            EcoPointsReward = categories.Where(c => c.Id == e.CategoryId).Select(c => c.EcoPointsReward).FirstOrDefault(),
            Date = e.Date,
            EndDate = e.EndDate,
            Address = e.Address,
            Location = e.Location,
            Capacity = e.Capacity,
            ActiveApplications = applications.Count(a => a.EventId == e.Id &&
                (a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Confirmed)),
            DistanceMeters = e.Location.Distance(userPoint)
        });
    }

    private static Point? ResolveUserPoint(EventListQuery query)
    {
        if (query.UserLat.HasValue != query.UserLon.HasValue)
            throw new RequestValidationException(nameof(query.UserLat), "Параметры userLat и userLon должны передаваться вместе.");

        if (query.SortBy == EventSortBy.Distance && !query.UserLat.HasValue)
            throw new RequestValidationException(nameof(query.SortBy), "Для сортировки по расстоянию необходимо передать userLat и userLon.");

        return query.UserLat is { } lat && query.UserLon is { } lon
            ? GeoLocation.CreatePoint(lat, lon)
            : null;
    }

    private sealed class EventRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = null!;
        public Guid CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public int EcoPointsReward { get; init; }
        public DateTimeOffset Date { get; init; }
        public DateTimeOffset EndDate { get; init; }
        public string Address { get; init; } = null!;
        public Point Location { get; init; } = null!;
        public int Capacity { get; init; }
        public int ActiveApplications { get; init; }
        public double? DistanceMeters { get; init; }
    }
}
