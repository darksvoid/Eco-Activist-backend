using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcoActivist.Application.Applications;

public sealed class VolunteerApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VolunteerApplicationService> _logger;

    public VolunteerApplicationService(
        IApplicationDbContext db,
        TimeProvider timeProvider,
        ILogger<VolunteerApplicationService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApplicationDto> ApplyAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        var application = await _db.ExecuteInTransactionAsync(async token =>
        {
            // Pessimistic lock on the event row: SELECT ... FOR UPDATE.
            // All concurrent applications for the same event are serialized here, so the capacity check below is race-free.
            if (!await _db.LockForUpdateAsync<Event>(eventId, token))
                throw NotFoundException.For<Event>(eventId);

            var ev = await _db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId, token);
            var now = _timeProvider.GetUtcNow();

            if (!ev.IsAcceptingApplications(now))
                throw new DomainException(ev.Status == EventStatus.Published
                    ? "Приём заявок на мероприятие завершён (за 24 часа до начала)."
                    : "Мероприятие не принимает заявки.");

            var hasActiveApplication = await _db.EventApplications
                .Where(a => a.EventId == eventId && a.UserId == userId)
                .WhereActive()
                .AnyAsync(token);

            if (hasActiveApplication)
                throw new ConflictException("У вас уже есть активная заявка на это мероприятие.");

            var activeApplications = await _db.EventApplications
                .Where(a => a.EventId == eventId)
                .WhereActive()
                .CountAsync(token);

            if (ev.Capacity - activeApplications <= 0)
                throw new ConflictException("Свободных мест на мероприятии нет.");

            var created = EventApplication.Create(userId, eventId, now);
            _db.EventApplications.Add(created);
            await _db.SaveChangesAsync(token);

            return created;
        }, cancellationToken: ct);

        _logger.LogInformation("Пользователь {UserId} подал заявку {ApplicationId} на мероприятие {EventId}",
            userId, application.Id, eventId);

        return application.ToDto();
    }

    public async Task<ApplicationDto> CancelAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        return await _db.ExecuteInTransactionAsync(async token =>
        {
            if (!await _db.LockForUpdateAsync<EventApplication>(applicationId, token))
                throw NotFoundException.For<EventApplication>(applicationId);

            var application = await _db.EventApplications.FirstAsync(a => a.Id == applicationId, token);
            if (application.UserId != userId)
                throw NotFoundException.For<EventApplication>(applicationId);

            var ev = await _db.Events.AsNoTracking().FirstAsync(e => e.Id == application.EventId, token);
            var now = _timeProvider.GetUtcNow();

            ev.EnsureApplicationCanBeCancelled(now);
            application.Cancel(now);

            await _db.SaveChangesAsync(token);
            return application.ToDto();
        }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<MyApplicationDto>> GetMyApplicationsAsync(Guid userId, ApplicationStatus? status, CancellationToken ct)
    {
        var query = _db.EventApplications.AsNoTracking().Where(a => a.UserId == userId);
        if (status is { } s)
            query = query.Where(a => a.Status == s);

        var attendance = _db.AttendanceRecords;

        var rows = await query
            .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => new
            {
                Application = a,
                EventId = e.Id,
                EventTitle = e.Title,
                EventDate = e.Date,
                EventEndDate = e.EndDate,
                EventAddress = e.Address,
                EventStatus = e.Status,
                AttendanceStatus = attendance
                    .Where(r => r.EventId == a.EventId && r.UserId == a.UserId)
                    .Select(r => (AttendanceStatus?)r.Status)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.EventDate)
            .ThenByDescending(x => x.Application.CreatedAt)
            .ToListAsync(ct);

        return rows
            .Select(r => new MyApplicationDto(
                r.Application.Id,
                r.Application.Status,
                r.Application.CreatedAt,
                r.Application.UpdatedAt,
                r.Application.RejectionReason,
                r.Application.Status == ApplicationStatus.Confirmed && r.Application.QrCodeToken != null,
                r.AttendanceStatus,
                new ApplicationEventSummaryDto(r.EventId, r.EventTitle, r.EventDate, r.EventEndDate, r.EventAddress, r.EventStatus)))
            .ToList();
    }

    public async Task<QrCodeDto> GetQrCodeAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        var application = await _db.EventApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, ct)
            ?? throw NotFoundException.For<EventApplication>(applicationId);

        if (application.Status != ApplicationStatus.Confirmed || application.QrCodeToken is null || application.QrCodeGeneratedAt is null)
            throw new DomainException("QR-код доступен только для подтверждённых заявок.");

        return new QrCodeDto(application.Id, application.EventId, application.QrCodeToken, application.QrCodeGeneratedAt.Value);
    }
}
