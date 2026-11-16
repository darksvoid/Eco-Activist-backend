using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<EcoActivist.Domain.Entities.User>;
using IdentityVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace EcoActivist.Infrastructure.Security;

/// <summary>
/// PBKDF2 (HMAC-SHA512, 100 000 iterations, 128-bit salt) via ASP.NET Core Identity password hasher (format V3).
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityPasswordHasher _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _hasher.HashPassword(null!, password);
    }

    public PasswordVerificationOutcome Verify(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationOutcome.Failed;

        try
        {
            return _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword) switch
            {
                IdentityVerificationResult.Success => PasswordVerificationOutcome.Success,
                IdentityVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
                _ => PasswordVerificationOutcome.Failed
            };
        }
        catch (FormatException)
        {
            return PasswordVerificationOutcome.Failed;
        }
    }
}
