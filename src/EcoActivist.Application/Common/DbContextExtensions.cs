using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Common;

internal static class DbContextExtensions
{
    public static async Task EnsureApprovedOrganizerAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct)
    {
        var isApproved = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Organizer && u.Status == OrganizerStatus.Approved, ct);

        if (!isApproved)
            throw new ForbiddenAccessException("Действие доступно только одобренным организаторам.");
    }

    public static async Task<Event> GetOwnedEventAsync(this IApplicationDbContext db, Guid eventId, Guid organizerId, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
                 ?? throw NotFoundException.For<Event>(eventId);

        if (!ev.IsOwnedBy(organizerId))
            throw new ForbiddenAccessException("Мероприятие принадлежит другому организатору.");

        return ev;
    }

    public static IQueryable<EventApplication> WhereActive(this IQueryable<EventApplication> query) =>
        query.Where(a => a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Confirmed);
}
