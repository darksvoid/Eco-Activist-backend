namespace EcoActivist.Application.Common.Security;

public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string OrganizerStatus = "organizer_status";
}

public static class AuthorizationPolicies
{
    public const string RequireVolunteer = nameof(RequireVolunteer);
    public const string RequireApprovedOrganizer = nameof(RequireApprovedOrganizer);
    public const string RequireAdmin = nameof(RequireAdmin);
}
