using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class EventApplicationConfiguration : IEntityTypeConfiguration<EventApplication>
{
    public void Configure(EntityTypeBuilder<EventApplication> builder)
    {
        builder.ToTable("EventApplications");

        builder.HasKey(a => a.Id);
        builder.Ignore(a => a.DomainEvents);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.QrCodeToken)
            .HasMaxLength(EventApplication.QrCodeTokenMaxLength);

        builder.Property(a => a.RejectionReason)
            .HasMaxLength(EventApplication.RejectionReasonMaxLength);

        // Only one active (Pending/Confirmed) application per user and event; historical ones are allowed.
        builder.HasIndex(a => new { a.UserId, a.EventId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Confirmed')")
            .HasDatabaseName(DbConstraints.ActiveApplicationUnique);

        builder.HasIndex(a => new { a.EventId, a.Status });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
