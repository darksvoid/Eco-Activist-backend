using System.Data;
using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Event> Events { get; }
    DbSet<EventApplication> EventApplications { get; }
    DbSet<Category> Categories { get; }
    DbSet<Article> Articles { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<EcoPointTransaction> EcoPointTransactions { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the operation inside a database transaction using the configured execution strategy.
    /// The operation may be re-executed on transient failures, so it must be idempotent with respect to in-memory state.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a pessimistic row lock (<c>SELECT ... FOR UPDATE</c>) on the entity row. Must be called inside a transaction.
    /// </summary>
    /// <returns><c>false</c> when the row does not exist.</returns>
    Task<bool> LockForUpdateAsync<TEntity>(Guid id, CancellationToken cancellationToken = default)
        where TEntity : Entity;

    /// <summary>
    /// Locks (<c>SELECT ... FOR UPDATE</c>) all applications of the event in the given status and returns their identifiers.
    /// </summary>
    Task<IReadOnlyList<Guid>> LockApplicationsForUpdateAsync(
        Guid eventId,
        ApplicationStatus status,
        CancellationToken cancellationToken = default);
}
