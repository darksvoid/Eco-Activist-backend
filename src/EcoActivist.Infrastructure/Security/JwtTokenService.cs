using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Security;
using EcoActivist.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EcoActivist.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key, at least 256 bits.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60 * 24 * 30)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false, MapInboundClaims = false };

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _signingCredentials = new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256);
        _validationParameters = CreateValidationParameters(_options, timeProvider);
    }

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, user.Id.ToString()),
            new(AppClaimTypes.Email, user.Email),
            new(AppClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (user.Status is { } organizerStatus)
            claims.Add(new Claim(AppClaimTypes.OrganizerStatus, organizerStatus.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public async Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var result = await _handler.ValidateTokenAsync(token, _validationParameters);
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options, TimeProvider timeProvider) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(options),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = AppClaimTypes.Subject,
        RoleClaimType = AppClaimTypes.Role,
        LifetimeValidator = (notBefore, expires, _, parameters) =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (expires is null)
                return false;
            if (notBefore is { } nbf && now.Add(parameters.ClockSkew) < nbf)
                return false;
            return now.Subtract(parameters.ClockSkew) < expires.Value;
        }
    };

    private static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}
