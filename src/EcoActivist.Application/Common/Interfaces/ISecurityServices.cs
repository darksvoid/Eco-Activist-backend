using System.Security.Claims;
using EcoActivist.Domain.Entities;

namespace EcoActivist.Application.Common.Interfaces;

public enum PasswordVerificationOutcome
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationOutcome Verify(string passwordHash, string providedPassword);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);

    Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record QrTokenPayload(Guid ApplicationId, Guid EventId, Guid UserId, DateTimeOffset IssuedAt);

public interface IQrTokenService
{
    string Generate(Guid applicationId, Guid eventId, Guid userId, DateTimeOffset issuedAt);

    bool TryValidate(string token, out QrTokenPayload? payload);
}
