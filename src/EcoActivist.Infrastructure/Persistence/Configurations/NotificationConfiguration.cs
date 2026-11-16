using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);
        builder.Ignore(n => n.DomainEvents);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(Notification.TitleMaxLength);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(Notification.MessageMaxLength);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.IsRead)
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
