using System.ComponentModel.DataAnnotations;
using EcoActivist.Application.Common.Models;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;

namespace EcoActivist.Application.Events;

public enum EventSortBy
{
    Date = 0,
    AvailablePlaces = 1,
    Distance = 2
}

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}

public sealed class EventListQuery : PageRequest
{
    [MaxLength(Event.TitleMaxLength)]
    public string? Search { get; init; }

    public Guid? CategoryId { get; init; }

    public DateTimeOffset? DateFrom { get; init; }

    public DateTimeOffset? DateTo { get; init; }

    public EventSortBy SortBy { get; init; } = EventSortBy.Date;

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;

    [Range(-90d, 90d)]
    public double? UserLat { get; init; }

    [Range(-180d, 180d)]
    public double? UserLon { get; init; }
}

public sealed record EventListItemDto(
    Guid Id,
    string Title,
    Guid CategoryId,
    string? CategoryName,
    int EcoPointsReward,
    DateTimeOffset Date,
    DateTimeOffset EndDate,
    string Address,
    GeoPointDto Location,
    int Capacity,
    int AvailablePlaces,
    double? DistanceMeters);

public sealed record EventDetailsDto(
    Guid Id,
    string Title,
    string Description,
    Guid CategoryId,
    string? CategoryName,
    int EcoPointsReward,
    DateTimeOffset Date,
    DateTimeOffset EndDate,
    DateTimeOffset ApplicationDeadline,
    string Address,
    GeoPointDto Location,
    string? ParticipationConditions,
    int Capacity,
    int AvailablePlaces,
    EventStatus Status,
    Guid OrganizerId);

public sealed class CreateEventRequest
{
    [Required, MaxLength(Event.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(Event.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    public DateTimeOffset Date { get; init; }

    [Required]
    public DateTimeOffset EndDate { get; init; }

    [Range(-90d, 90d)]
    public double Latitude { get; init; }

    [Range(-180d, 180d)]
    public double Longitude { get; init; }

    [Required, MaxLength(Event.AddressMaxLength)]
    public string Address { get; init; } = string.Empty;

    [MaxLength(Event.ParticipationConditionsMaxLength)]
    public string? ParticipationConditions { get; init; }

    [Range(1, 100_000)]
    public int Capacity { get; init; }
}

public sealed class CancelEventRequest
{
    [Required, MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record OrganizerEventDto(
    Guid Id,
    string Title,
    DateTimeOffset Date,
    DateTimeOffset EndDate,
    EventStatus Status,
    int Capacity,
    int PendingApplications,
    int ConfirmedApplications,
    int AvailablePlaces);
