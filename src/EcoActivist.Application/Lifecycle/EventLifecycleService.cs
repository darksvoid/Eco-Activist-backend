using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcoActivist.Application.Lifecycle;

public sealed record EventLifecycleResult(int ExpiredApplications, int RemindersCreated, int AbsencesRecorded, int EventsCompleted);

/// <summary>
/// Periodic event lifecycle processing:
/// 1) for events starting within 24 hours: expires unreviewed (Pending) applications and creates reminders for confirmed participants;
/// 2) for events whose attendance window (EndDate + 48h) has closed: records Absent for unmarked participants and completes the event.
/// Each event is processed in its own transaction under a row lock so that a failure for one event does not affect others.
/// </summary>
public sealed class EventLifecycleService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventLifecycleService> _logger;

    public EventLifecycleService(IApplicationDbContext db, TimeProvider timeProvider, ILogger<EventLifecycleService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<EventLifecycleResult> ProcessAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var expired = 0;
        var reminders = 0;
        var absences = 0;
        var completed = 0;

        var upcomingThreshold = now + Event.ApplicationReviewCutoff;
        var applications = _db.EventApplications;

        var upcomingEventIds = await _db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.Date <= upcomingThreshold && e.EndDate > now)
            .Where(e => applications.Any(a => a.EventId == e.Id &&
                (a.Status == ApplicationStatus.Pending ||
                 (a.Status == ApplicationStatus.Confirmed && a.ReminderSentAt == null))))
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var eventId in upcomingEventIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (e, r) = await ProcessUpcomingEventAsync(eventId, ct);
                expired += e;
                reminders += r;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка обработки предстоящего мероприятия {EventId}", eventId);
            }
        }

        var attendanceDeadline = now - Event.AttendanceMarkingGracePeriod;

        var finishedEventIds = await _db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.EndDate < attendanceDeadline)
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var eventId in finishedEventIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                absences += await FinalizeEventAsync(eventId, ct);
                completed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка финализации мероприятия {EventId}", eventId);
            }
        }

        var result = new EventLifecycleResult(expired, reminders, absences, completed);
        if (expired + reminders + absences + completed > 0)
            _logger.LogInformation("Жизненный цикл мероприятий обработан: {@Result}", result);

        return result;
    }

    private Task<(int Expired, int Reminders)> ProcessUpcomingEventAsync(Guid eventId, CancellationToken ct) =>
        _db.ExecuteInTransactionAsync(async tx =>
        {
            if (!await _db.LockForUpdateAsync<Event>(eventId, tx))
                return (0, 0);

            var ev = await _db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId, tx);
            var now = _timeProvider.GetUtcNow();
            if (ev.Status != EventStatus.Published)
                return (0, 0);

            var pendingIds = await _db.LockApplicationsForUpdateAsync(eventId, ApplicationStatus.Pending, tx);
            var pending = await _db.EventApplications.Where(a => pendingIds.Contains(a.Id)).ToListAsync(tx);
            foreach (var application in pending)
                application.Expire(now);

            var reminders = 0;
            if (!ev.HasStarted(now))
            {
                var confirmed = await _db.EventApplications
                    .Where(a => a.EventId == eventId && a.Status == ApplicationStatus.Confirmed && a.ReminderSentAt == null)
                    .ToListAsync(tx);

                foreach (var application in confirmed)
                {
                    application.MarkReminderSent(now);
                    _db.Notifications.Add(Notification.Create(
                        application.UserId,
                        "Напоминание о мероприятии",
                        $"Мероприятие «{ev.Title}» начнётся {ev.Date:dd.MM.yyyy HH:mm} (UTC{ev.Date:zzz}) по адресу: {ev.Address}. Не забудьте QR-код для отметки явки.",
                        now));
                    reminders++;
                }
            }

            await _db.SaveChangesAsync(tx);
            return (pending.Count, reminders);
        }, cancellationToken: ct);

    private Task<int> FinalizeEventAsync(Guid eventId, CancellationToken ct) =>
        _db.ExecuteInTransactionAsync(async tx =>
        {
            if (!await _db.LockForUpdateAsync<Event>(eventId, tx))
                return 0;

            var ev = await _db.Events.FirstAsync(e => e.Id == eventId, tx);
            var now = _timeProvider.GetUtcNow();
            if (ev.Status != EventStatus.Published || !ev.IsAttendanceWindowClosed(now))
                return 0;

            var attendance = _db.AttendanceRecords;
            var unmarkedUserIds = await _db.EventApplications.AsNoTracking()
                .Where(a => a.EventId == eventId && a.Status == ApplicationStatus.Confirmed)
                .Where(a => !attendance.Any(r => r.EventId == eventId && r.UserId == a.UserId))
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync(tx);

            foreach (var userId in unmarkedUserIds)
                _db.AttendanceRecords.Add(AttendanceRecord.MarkAbsent(eventId, userId, ev.OrganizerId, now));

            var stalePending = await _db.EventApplications
                .Where(a => a.EventId == eventId && a.Status == ApplicationStatus.Pending)
                .ToListAsync(tx);
            foreach (var application in stalePending)
                application.Expire(now);

            ev.Complete();
            await _db.SaveChangesAsync(tx);
            return unmarkedUserIds.Count;
        }, cancellationToken: ct);
}
