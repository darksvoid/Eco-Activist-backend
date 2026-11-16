using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", t =>
        {
            t.HasCheckConstraint("CK_Events_EndDate_After_Date", "\"EndDate\" > \"Date\"");
            t.HasCheckConstraint("CK_Events_Capacity_Positive", "\"Capacity\" > 0");
        });

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(Event.TitleMaxLength);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(Event.DescriptionMaxLength);

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.EndDate)
            .IsRequired();

        builder.Property(e => e.Address)
            .IsRequired()
            .HasMaxLength(Event.AddressMaxLength);

        builder.Property(e => e.ParticipationConditions)
            .HasMaxLength(Event.ParticipationConditionsMaxLength);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // geography: ST_Distance returns metres on the WGS84 spheroid.
        builder.Property(e => e.Location)
            .HasColumnType("geography(Point, 4326)")
            .IsRequired();

        builder.Property(e => e.Capacity)
            .IsRequired();

        builder.Property(e => e.OrganizerId)
            .IsRequired();

        builder.Property(e => e.CategoryId)
            .IsRequired();

        builder.HasIndex(e => e.Location)
            .HasMethod("gist");

        builder.HasIndex(e => new { e.Status, e.Date });
        builder.HasIndex(e => e.OrganizerId);
        builder.HasIndex(e => e.CategoryId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.OrganizerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
