using System.Data;
using EcoActivist.Application.Common;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EcoActivist.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private static readonly IReadOnlyDictionary<string, string> UniqueConstraintMessages = new Dictionary<string, string>
    {
        [DbConstraints.UsersEmailUnique] = "Пользователь с таким email уже зарегистрирован.",
        [DbConstraints.ActiveApplicationUnique] = "У пользователя уже есть активная заявка на это мероприятие.",
        [DbConstraints.AttendanceEventUserUnique] = "Явка участника уже зафиксирована.",
        [DbConstraints.EcoPointTransactionAttendanceUnique] = "Баллы за эту явку уже начислены.",
        [DbConstraints.CategoriesNameUnique] = "Категория с таким названием уже существует."
    };

    private readonly IPublisher? _publisher;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventApplication> EventApplications => Set<EventApplication>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<EcoPointTransaction> EcoPointTransactions => Set<EcoPointTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            var message = pg.ConstraintName is not null && UniqueConstraintMessages.TryGetValue(pg.ConstraintName, out var mapped)
                ? mapped
                : "Запись нарушает ограничение уникальности.";
            throw new ConflictException(message, ex);
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Database.CurrentTransaction is not null)
            return await operation(cancellationToken);

        var attempt = 0;
        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            if (attempt++ > 0)
                ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(isolationLevel, ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public async Task<bool> LockForUpdateAsync<TEntity>(Guid id, CancellationToken cancellationToken = default)
        where TEntity : Entity
    {
        EnsureTransaction();

        var entityType = Model.FindEntityType(typeof(TEntity))
                         ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' is not part of the model.");

        var tableName = entityType.GetTableName()
                        ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' is not mapped to a table.");
        var schema = entityType.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(tableName, schema);
        var idColumn = entityType.FindProperty(nameof(Entity.Id))!.GetColumnName(storeObject)
                       ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' has no Id column.");

        var sql = this.GetService<ISqlGenerationHelper>();
        var quotedId = sql.DelimitIdentifier(idColumn);
        var query = $"SELECT {quotedId} AS \"Value\" FROM {sql.DelimitIdentifier(tableName, schema)} WHERE {quotedId} = {{0}} FOR UPDATE";

        var locked = await Database.SqlQueryRaw<Guid>(query, id).ToListAsync(cancellationToken);
        return locked.Count > 0;
    }

    public async Task<IReadOnlyList<Guid>> LockApplicationsForUpdateAsync(
        Guid eventId,
        ApplicationStatus status,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();

        return await Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM "EventApplications"
                WHERE "EventId" = {eventId} AND "Status" = {status.ToString()}
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
    }

    private void EnsureTransaction()
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pessimistic row locks require an active database transaction.");
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        if (_publisher is null)
            return;

        // Handlers may add new entities (e.g. notifications) which in turn may raise events, so drain until empty.
        while (true)
        {
            var domainEvents = ChangeTracker.Entries<AggregateRoot>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .SelectMany(e =>
                {
                    var events = e.DomainEvents.ToList();
                    e.ClearDomainEvents();
                    return events;
                })
                .ToList();

            if (domainEvents.Count == 0)
                return;

            foreach (var domainEvent in domainEvents)
            {
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
                if (Activator.CreateInstance(notificationType, domainEvent) is INotification notification)
                {
                    await _publisher.Publish(notification, cancellationToken);
                }
            }
        }
    }
}
