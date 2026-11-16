using System.ComponentModel.DataAnnotations;
using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcoActivist.Application.Attendance;

public sealed class MarkAttendanceByQrRequest
{
    [Required, MaxLength(EventApplication.QrCodeTokenMaxLength)]
    public string Token { get; init; } = string.Empty;
}

public sealed class MarkAttendanceManualRequest
{
    [Required]
    public Guid UserId { get; init; }
}

public sealed record AttendanceResultDto(
    Guid AttendanceRecordId,
    Guid EventId,
    Guid UserId,
    Guid ApplicationId,
    AttendanceStatus Status,
    DateTimeOffset MarkedAt,
    int EcoPointsAwarded,
    int EcoPointsBalance);

public sealed class AttendanceService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrTokenService _qrTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IApplicationDbContext db,
        IQrTokenService qrTokenService,
        TimeProvider timeProvider,
        ILogger<AttendanceService> logger)
    {
        _db = db;
        _qrTokenService = qrTokenService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AttendanceResultDto> MarkByQrAsync(Guid organizerId, Guid eventId, string token, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        var result = await _db.ExecuteInTransactionAsync(async tx =>
        {
            var ev = await LoadEventForMarkingAsync(organizerId, eventId, tx);

            if (!_qrTokenService.TryValidate(token, out var payload) || payload is null)
                throw new DomainException("QR-код недействителен.");

            if (payload.EventId != eventId)
                throw new DomainException("QR-код выдан для другого мероприятия.");

            if (!await _db.LockForUpdateAsync<EventApplication>(payload.ApplicationId, tx))
                throw new DomainException("QR-код недействителен.");

            var application = await _db.EventApplications.FirstAsync(a => a.Id == payload.ApplicationId, tx);

            if (application.EventId != eventId || application.UserId != payload.UserId)
                throw new DomainException("QR-код недействителен.");

            if (await IsAttendanceAlreadyMarkedAsync(eventId, application.UserId, tx))
                throw new ConflictException("Явка участника уже зафиксирована.");

            if (!application.MatchesQrToken(token))
                throw new DomainException("QR-код отозван или заменён.");

            return await MarkAttendedAsync(ev, application, organizerId, tx);
        }, cancellationToken: ct);

        LogMarked(result, "QR");
        return result;
    }

    public async Task<AttendanceResultDto> MarkManualAsync(Guid organizerId, Guid eventId, Guid userId, CancellationToken ct)
    {
        await _db.EnsureApprovedOrganizerAsync(organizerId, ct);

        var result = await _db.ExecuteInTransactionAsync(async tx =>
        {
            var ev = await LoadEventForMarkingAsync(organizerId, eventId, tx);

            if (await IsAttendanceAlreadyMarkedAsync(eventId, userId, tx))
                throw new ConflictException("Явка участника уже зафиксирована.");

            var applicationId = await _db.EventApplications.AsNoTracking()
                .Where(a => a.EventId == eventId && a.UserId == userId && a.Status == ApplicationStatus.Confirmed)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync(tx)
                ?? throw new DomainException("У пользователя нет подтверждённой заявки на это мероприятие.");

            if (!await _db.LockForUpdateAsync<EventApplication>(applicationId, tx))
                throw NotFoundException.For<EventApplication>(applicationId);

            var application = await _db.EventApplications.FirstAsync(a => a.Id == applicationId, tx);

            return await MarkAttendedAsync(ev, application, organizerId, tx);
        }, cancellationToken: ct);

        LogMarked(result, "manual");
        return result;
    }

    private async Task<Event> LoadEventForMarkingAsync(Guid organizerId, Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct)
                 ?? throw NotFoundException.For<Event>(eventId);

        if (!ev.IsOwnedBy(organizerId))
            throw new ForbiddenAccessException("Отмечать явку может только организатор мероприятия.");

        ev.EnsureAttendanceWindowOpen(_timeProvider.GetUtcNow());
        return ev;
    }

    private Task<bool> IsAttendanceAlreadyMarkedAsync(Guid eventId, Guid userId, CancellationToken ct) =>
        _db.AttendanceRecords.AnyAsync(r => r.EventId == eventId && r.UserId == userId, ct);

    private async Task<AttendanceResultDto> MarkAttendedAsync(Event ev, EventApplication application, Guid organizerId, CancellationToken ct)
    {
        // Re-check after the application row lock has been acquired: a concurrent request may have committed in between.
        if (application.Status != ApplicationStatus.Confirmed)
            throw new DomainException("Явку можно отметить только по подтверждённой заявке.");

        if (await IsAttendanceAlreadyMarkedAsync(ev.Id, application.UserId, ct))
            throw new ConflictException("Явка участника уже зафиксирована.");

        var reward = await _db.Categories.AsNoTracking()
            .Where(c => c.Id == ev.CategoryId)
            .Select(c => c.EcoPointsReward)
            .FirstOrDefaultAsync(ct);

        if (!await _db.LockForUpdateAsync<User>(application.UserId, ct))
            throw NotFoundException.For<User>(application.UserId);

        var user = await _db.Users.FirstAsync(u => u.Id == application.UserId, ct);
        var now = _timeProvider.GetUtcNow();

        var record = AttendanceRecord.MarkAttended(ev.Id, application.UserId, organizerId, now);
        _db.AttendanceRecords.Add(record);

        if (reward > 0)
        {
            _db.EcoPointTransactions.Add(EcoPointTransaction.ForAttendance(
                user.Id,
                reward,
                $"Участие в мероприятии «{ev.Title}»",
                now,
                record.Id));

            user.AddEcoPoints(reward);

            _db.Notifications.Add(Notification.Create(
                user.Id,
                "Начислены ЭкоБаллы",
                $"За участие в мероприятии «{ev.Title}» вам начислено {reward} ЭкоБаллов.",
                now));
        }

        await _db.SaveChangesAsync(ct);

        return new AttendanceResultDto(
            record.Id,
            ev.Id,
            user.Id,
            application.Id,
            record.Status,
            record.MarkedAt,
            reward,
            user.EcoPointsBalance);
    }

    private void LogMarked(AttendanceResultDto result, string method) =>
        _logger.LogInformation(
            "Явка пользователя {UserId} на мероприятии {EventId} отмечена ({Method}), начислено {Points} ЭкоБаллов",
            result.UserId, result.EventId, method, result.EcoPointsAwarded);
}
