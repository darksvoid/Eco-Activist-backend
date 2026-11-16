using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t => t.HasCheckConstraint("CK_Users_EcoPointsBalance_NonNegative", "\"EcoPointsBalance\" >= 0"));

        builder.HasKey(u => u.Id);
        builder.Ignore(u => u.DomainEvents);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(User.EmailMaxLength);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName(DbConstraints.UsersEmailUnique);

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(User.PhoneMaxLength);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.EcoPointsBalance)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.HasIndex(u => new { u.Role, u.Status });
    }
}
