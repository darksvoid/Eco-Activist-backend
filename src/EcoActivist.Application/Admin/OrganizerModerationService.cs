using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Admin;

public sealed record OrganizerDto(Guid Id, string Email, string Phone, OrganizerStatus? Status, DateTimeOffset CreatedAt);

public sealed class OrganizerModerationService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public OrganizerModerationService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<OrganizerDto>> GetOrganizersAsync(OrganizerStatus? status, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.Role == UserRole.Organizer);
        if (status is { } s)
            query = query.Where(u => u.Status == s);

        return await query
            .OrderBy(u => u.CreatedAt)
            .Select(u => new OrganizerDto(u.Id, u.Email, u.Phone, u.Status, u.CreatedAt))
            .ToListAsync(ct);
    }

    public Task<OrganizerDto> ApproveAsync(Guid organizerId, CancellationToken ct) =>
        ChangeStatusAsync(organizerId, u => u.ApproveOrganizer(), "Ваша заявка организатора одобрена. Перезайдите в систему для обновления прав.", ct);

    public Task<OrganizerDto> RejectAsync(Guid organizerId, CancellationToken ct) =>
        ChangeStatusAsync(organizerId, u => u.RejectOrganizer(), "Ваша заявка организатора отклонена.", ct);

    public Task<OrganizerDto> SendToRevisionAsync(Guid organizerId, CancellationToken ct) =>
        ChangeStatusAsync(organizerId, u => u.SendOrganizerToRevision(), "Ваша заявка организатора отправлена на доработку.", ct);

    private async Task<OrganizerDto> ChangeStatusAsync(Guid organizerId, Action<User> change, string message, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == organizerId && u.Role == UserRole.Organizer, ct)
                   ?? throw NotFoundException.For<User>(organizerId);

        change(user);
        _db.Notifications.Add(Notification.Create(user.Id, "Статус организатора изменён", message, _timeProvider.GetUtcNow()));
        await _db.SaveChangesAsync(ct);

        return new OrganizerDto(user.Id, user.Email, user.Phone, user.Status, user.CreatedAt);
    }
}
