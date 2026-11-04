using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EcoActivist.Application.Common;

namespace EcoActivist.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventApplication> EventApplications => Set<EventApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        if (_publisher != null)
        {
            foreach (var domainEvent in domainEvents)
            {
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
                if (Activator.CreateInstance(notificationType, domainEvent) is INotification notification)
                {
                    await _publisher.Publish(notification, cancellationToken);
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}