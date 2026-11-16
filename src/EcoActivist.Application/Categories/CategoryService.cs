using System.ComponentModel.DataAnnotations;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Categories;

public sealed class UpsertCategoryRequest
{
    [Required, MaxLength(Category.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 100_000)]
    public int EcoPointsReward { get; init; }
}

public sealed record CategoryDto(Guid Id, string Name, int EcoPointsReward);

public sealed class CategoryService
{
    private readonly IApplicationDbContext _db;

    public CategoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct) =>
        await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.EcoPointsReward))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateAsync(UpsertCategoryRequest request, CancellationToken ct)
    {
        var category = Category.Create(request.Name, request.EcoPointsReward);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.EcoPointsReward);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpsertCategoryRequest request, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw NotFoundException.For<Category>(id);

        category.Update(request.Name, request.EcoPointsReward);
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.EcoPointsReward);
    }
}
