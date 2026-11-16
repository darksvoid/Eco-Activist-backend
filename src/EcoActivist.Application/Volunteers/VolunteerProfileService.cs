using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Models;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Volunteers;

public sealed record EcoPointTransactionDto(Guid Id, int Amount, string Reason, DateTimeOffset CreatedAt, Guid? AttendanceRecordId);

public sealed record AttendanceStatisticsDto(
    int AttendedCount,
    int AbsentCount,
    int UpcomingConfirmedCount,
    int PendingApplicationsCount,
    int TotalEcoPointsEarned);

public sealed record VolunteerProfileDto(
    Guid UserId,
    string Email,
    string Phone,
    int EcoPointsBalance,
    AttendanceStatisticsDto Statistics,
    PagedResult<EcoPointTransactionDto> Transactions);

public sealed class VolunteerProfileService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public VolunteerProfileService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<VolunteerProfileDto> GetProfileAsync(Guid userId, PageRequest transactionsPage, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.Phone, u.EcoPointsBalance })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For<User>(userId);

        var attendance = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.UserId == userId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var now = _timeProvider.GetUtcNow();

        var upcomingConfirmed = await _db.EventApplications.AsNoTracking()
            .Where(a => a.UserId == userId && a.Status == ApplicationStatus.Confirmed)
            .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e)
            .CountAsync(e => e.Date > now, ct);

        var pending = await _db.EventApplications.AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.Status == ApplicationStatus.Pending, ct);

        var transactions = _db.EcoPointTransactions.AsNoTracking().Where(t => t.UserId == userId);

        var totalEarned = await transactions.Where(t => t.Amount > 0).SumAsync(t => (int?)t.Amount, ct) ?? 0;
        var transactionsCount = await transactions.CountAsync(ct);

        var items = await transactions
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip(transactionsPage.Skip)
            .Take(transactionsPage.PageSize)
            .Select(t => new EcoPointTransactionDto(t.Id, t.Amount, t.Reason, t.CreatedAt, t.AttendanceRecordId))
            .ToListAsync(ct);

        var statistics = new AttendanceStatisticsDto(
            attendance.Where(a => a.Status == AttendanceStatus.Attended).Sum(a => a.Count),
            attendance.Where(a => a.Status == AttendanceStatus.Absent).Sum(a => a.Count),
            upcomingConfirmed,
            pending,
            totalEarned);

        return new VolunteerProfileDto(
            user.Id,
            user.Email,
            user.Phone,
            user.EcoPointsBalance,
            statistics,
            new PagedResult<EcoPointTransactionDto>(items, transactionsPage.Page, transactionsPage.PageSize, transactionsCount));
    }
}
