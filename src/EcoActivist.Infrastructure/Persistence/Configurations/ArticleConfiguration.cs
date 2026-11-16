using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoActivist.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");

        builder.HasKey(a => a.Id);
        builder.Ignore(a => a.DomainEvents);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(Article.TitleMaxLength);

        builder.Property(a => a.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(a => a.Category)
            .HasMaxLength(Article.CategoryMaxLength);

        builder.Property(a => a.IsPublished)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasIndex(a => new { a.IsPublished, a.CreatedAt });
        builder.HasIndex(a => a.Category);
    }
}
