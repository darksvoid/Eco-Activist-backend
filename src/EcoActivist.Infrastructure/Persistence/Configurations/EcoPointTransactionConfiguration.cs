using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class EcoPointTransactionConfiguration : IEntityTypeConfiguration<EcoPointTransaction>
{
    public void Configure(EntityTypeBuilder<EcoPointTransaction> builder)
    {
        builder.ToTable("EcoPointTransactions", t =>
            t.HasCheckConstraint("CK_EcoPointTransactions_Amount_NonZero", "\"Amount\" <> 0"));

        builder.HasKey(t => t.Id);
        builder.Ignore(t => t.DomainEvents);

        builder.Property(t => t.Amount)
            .IsRequired();

        builder.Property(t => t.Reason)
            .IsRequired()
            .HasMaxLength(EcoPointTransaction.ReasonMaxLength);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.CreatedAt });

        // Points can be awarded for a particular attendance record at most once.
        builder.HasIndex(t => t.AttendanceRecordId)
            .IsUnique()
            .HasFilter("\"AttendanceRecordId\" IS NOT NULL")
            .HasDatabaseName(DbConstraints.EcoPointTransactionAttendanceUnique);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AttendanceRecord>()
            .WithMany()
            .HasForeignKey(t => t.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
