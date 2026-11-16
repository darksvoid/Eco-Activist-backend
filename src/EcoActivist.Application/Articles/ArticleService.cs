using System.ComponentModel.DataAnnotations;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Models;
using EcoActivist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Application.Articles;

public sealed class ArticleListQuery : PageRequest
{
    [MaxLength(Article.CategoryMaxLength)]
    public string? Category { get; init; }
}

public sealed class UpsertArticleRequest
{
    [Required, MaxLength(Article.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(100_000)]
    public string Content { get; init; } = string.Empty;

    [MaxLength(Article.CategoryMaxLength)]
    public string? Category { get; init; }

    public bool IsPublished { get; init; }
}

public sealed record ArticleSummaryDto(Guid Id, string Title, string? Category, bool IsPublished, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record ArticleDto(Guid Id, string Title, string Content, string? Category, bool IsPublished, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed class ArticleService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ArticleService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public Task<PagedResult<ArticleSummaryDto>> GetPublishedAsync(ArticleListQuery query, CancellationToken ct) =>
        GetPageAsync(query, publishedOnly: true, ct);

    public Task<PagedResult<ArticleSummaryDto>> GetAllAsync(ArticleListQuery query, CancellationToken ct) =>
        GetPageAsync(query, publishedOnly: false, ct);

    public async Task<ArticleDto> GetPublishedByIdAsync(Guid id, CancellationToken ct)
    {
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.IsPublished, ct)
                      ?? throw NotFoundException.For<Article>(id);
        return ToDto(article);
    }

    public async Task<ArticleDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw NotFoundException.For<Article>(id);
        return ToDto(article);
    }

    public async Task<ArticleDto> CreateAsync(UpsertArticleRequest request, CancellationToken ct)
    {
        var article = Article.Create(request.Title, request.Content, request.Category, request.IsPublished, _timeProvider.GetUtcNow());
        _db.Articles.Add(article);
        await _db.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task<ArticleDto> UpdateAsync(Guid id, UpsertArticleRequest request, CancellationToken ct)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw NotFoundException.For<Article>(id);

        article.Update(request.Title, request.Content, request.Category, request.IsPublished, _timeProvider.GetUtcNow());
        await _db.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await _db.Articles.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
            throw NotFoundException.For<Article>(id);
    }

    private async Task<PagedResult<ArticleSummaryDto>> GetPageAsync(ArticleListQuery query, bool publishedOnly, CancellationToken ct)
    {
        var articles = _db.Articles.AsNoTracking();
        if (publishedOnly)
            articles = articles.Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            articles = articles.Where(a => a.Category == category);
        }

        var total = await articles.CountAsync(ct);
        var items = await articles
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(a => new ArticleSummaryDto(a.Id, a.Title, a.Category, a.IsPublished, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<ArticleSummaryDto>(items, query.Page, query.PageSize, total);
    }

    private static ArticleDto ToDto(Article a) =>
        new(a.Id, a.Title, a.Content, a.Category, a.IsPublished, a.CreatedAt, a.UpdatedAt);
}
