using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public static readonly Guid CleanupCategoryId = Guid.Parse("6b1f0c1e-3c1a-4d55-9a0e-1f7f2b8c0a01");
    public static readonly Guid TreePlantingCategoryId = Guid.Parse("6b1f0c1e-3c1a-4d55-9a0e-1f7f2b8c0a02");
    public static readonly Guid RecyclingCategoryId = Guid.Parse("6b1f0c1e-3c1a-4d55-9a0e-1f7f2b8c0a03");
    public static readonly Guid EducationCategoryId = Guid.Parse("6b1f0c1e-3c1a-4d55-9a0e-1f7f2b8c0a04");

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", t =>
            t.HasCheckConstraint("CK_Categories_EcoPointsReward_NonNegative", "\"EcoPointsReward\" >= 0"));

        builder.HasKey(c => c.Id);
        builder.Ignore(c => c.DomainEvents);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(Category.NameMaxLength);

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName(DbConstraints.CategoriesNameUnique);

        builder.Property(c => c.EcoPointsReward)
            .IsRequired();

        builder.HasData(
            new { Id = CleanupCategoryId, Name = "Уборка территории", EcoPointsReward = 50 },
            new { Id = TreePlantingCategoryId, Name = "Посадка деревьев", EcoPointsReward = 100 },
            new { Id = RecyclingCategoryId, Name = "Раздельный сбор отходов", EcoPointsReward = 30 },
            new { Id = EducationCategoryId, Name = "Экопросвещение", EcoPointsReward = 20 });
    }
}
