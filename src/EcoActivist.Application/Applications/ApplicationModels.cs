using System.ComponentModel.DataAnnotations;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;

namespace EcoActivist.Application.Applications;

public sealed record ApplicationEventSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset Date,
    DateTimeOffset EndDate,
    string Address,
    EventStatus Status);

public sealed record MyApplicationDto(
    Guid Id,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? RejectionReason,
    bool HasQrCode,
    AttendanceStatus? AttendanceStatus,
    ApplicationEventSummaryDto Event);

public sealed record ApplicationDto(
    Guid Id,
    Guid EventId,
    Guid UserId,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? RejectionReason);

public sealed record QrCodeDto(Guid ApplicationId, Guid EventId, string Token, DateTimeOffset GeneratedAt);

public sealed record OrganizerApplicationDto(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string UserPhone,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? RejectionReason,
    AttendanceStatus? AttendanceStatus);

public sealed class RejectApplicationRequest
{
    [Required, MaxLength(EventApplication.RejectionReasonMaxLength)]
    public string Reason { get; init; } = string.Empty;
}

public static class ApplicationMappings
{
    public static ApplicationDto ToDto(this EventApplication application) => new(
        application.Id,
        application.EventId,
        application.UserId,
        application.Status,
        application.CreatedAt,
        application.UpdatedAt,
        application.RejectionReason);
}
