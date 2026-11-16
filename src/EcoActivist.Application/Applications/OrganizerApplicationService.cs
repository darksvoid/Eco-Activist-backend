using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Applications;

public sealed class OrganizerApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrTokenService _qrTokenService;
    private readonly TimeProvider _timeProvider;

    public OrganizerApplicationService(IApplicationDbContext db, IQrTokenService qrTokenService, TimeProvider timeProvider)
    {
        _db = db;
        _qrTokenService = qrTokenService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<OrganizerApplicationDto>> GetEventApplicationsAsync(
        Guid organizerId,
        Guid eventId,
        ApplicationStatus? status,
        CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        var ownerId = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => (Guid?)e.OrganizerId)
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For<Event>(eventId);

        if (ownerId != organizerId)
            throw new ForbiddenAccessException("Мероприятие принадлежит другому организатору.");

        var query = _db.EventApplications.AsNoTracking().Where(a => a.EventId == eventId);
        if (status is { } s)
            query = query.Where(a => a.Status == s);

        var attendance = _db.AttendanceRecords;

        return await query
            .OrderBy(a => a.CreatedAt)
            .Join(_db.Users, a => a.UserId, u => u.Id, (a, u) => new OrganizerApplicationDto(
                a.Id,
                u.Id,
                u.Email,
                u.Phone,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt,
                a.RejectionReason,
                attendance
                    .Where(r => r.EventId == a.EventId && r.UserId == a.UserId)
                    .Select(r => (AttendanceStatus?)r.Status)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ApplicationDto> ConfirmAsync(Guid organizerId, Guid applicationId, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        return await _db.ExecuteInTransactionAsync(async token =>
        {
            var (application, ev) = await LoadForReviewAsync(organizerId, applicationId, token);
            var now = _timeProvider.GetUtcNow();

            if (!ev.CanApplicationsBeReviewed(now))
                throw new DomainException("Рассмотрение заявок возможно только для опубликованного мероприятия до его начала.");

            var qrToken = _qrTokenService.Generate(application.Id, application.EventId, application.UserId, now);
            application.Confirm(qrToken, now);

            await _db.SaveChangesAsync(token);
            return application.ToDto();
        }, cancellationToken: ct);
    }

    public async Task<ApplicationDto> RejectAsync(Guid organizerId, Guid applicationId, string reason, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        return await _db.ExecuteInTransactionAsync(async token =>
        {
            var (application, ev) = await LoadForReviewAsync(organizerId, applicationId, token);
            var now = _timeProvider.GetUtcNow();

            if (ev.HasStarted(now))
                throw new DomainException("Нельзя отклонить заявку после начала мероприятия.");

            application.Reject(reason, now);

            await _db.SaveChangesAsync(token);
            return application.ToDto();
        }, cancellationToken: ct);
    }

    private async Task<(EventApplication Application, Event Event)> LoadForReviewAsync(
        Guid organizerId,
        Guid applicationId,
        CancellationToken ct)
    {
        if (!await _db.LockForUpdateAsync<EventApplication>(applicationId, ct))
            throw NotFoundException.For<EventApplication>(applicationId);

        var application = await _db.EventApplications.FirstAsync(a => a.Id == applicationId, ct);
        var ev = await _db.Events.AsNoTracking().FirstAsync(e => e.Id == application.EventId, ct);

        if (!ev.IsOwnedBy(organizerId))
            throw new ForbiddenAccessException("Заявка относится к мероприятию другого организатора.");

        return (application, ev);
    }
}
