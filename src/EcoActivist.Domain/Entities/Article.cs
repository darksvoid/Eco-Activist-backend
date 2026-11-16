using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Entities;

public class Article : AggregateRoot
{
    public const int TitleMaxLength = 300;
    public const int CategoryMaxLength = 100;

    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Article() { }

    public Article(Guid id, string title, string content, string? category, bool isPublished, DateTimeOffset createdAt) : base(id)
    {
        SetContent(title, content, category);
        IsPublished = isPublished;
        CreatedAt = createdAt;
    }

    public static Article Create(string title, string content, string? category, bool isPublished, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), title, content, category, isPublished, createdAt);

    public void Update(string title, string content, string? category, bool isPublished, DateTimeOffset now)
    {
        SetContent(title, content, category);
        IsPublished = isPublished;
        UpdatedAt = now;
    }

    private void SetContent(string title, string content, string? category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > TitleMaxLength)
            throw new ArgumentException($"Title must not exceed {TitleMaxLength} characters.", nameof(title));

        var trimmedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (trimmedCategory is { Length: > CategoryMaxLength })
            throw new ArgumentException($"Category must not exceed {CategoryMaxLength} characters.", nameof(category));

        Title = trimmedTitle;
        Content = content;
        Category = trimmedCategory;
    }
}
