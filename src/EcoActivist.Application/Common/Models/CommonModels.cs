using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace EcoActivist.Application.Common.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record GeoPointDto(double Latitude, double Longitude)
{
    public static GeoPointDto From(Point point) => new(point.Y, point.X);
}

public class PageRequest
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}
