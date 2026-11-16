using System.Security.Claims;
using EcoActivist.Application.Common.Security;

namespace EcoActivist.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppClaimTypes.Subject);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Токен не содержит идентификатор пользователя.");
    }
}
