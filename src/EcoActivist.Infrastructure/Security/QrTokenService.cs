using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcoActivist.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace EcoActivist.Infrastructure.Security;

public sealed class QrCodeOptions
{
    public const string SectionName = "QrCode";

    /// <summary>HMAC-SHA256 key used to sign attendance QR tokens, at least 256 bits.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}

/// <summary>
/// Token format: <c>base64url(payload) "." base64url(HMAC-SHA256(key, base64url(payload)))</c>,
/// payload: <c>v1|applicationId|eventId|userId|issuedAtUnixSeconds|nonce</c>.
/// </summary>
public sealed class QrTokenService : IQrTokenService
{
    private const string Version = "v1";
    private const char Separator = '|';

    private readonly byte[] _key;

    public QrTokenService(IOptions<QrCodeOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.SigningKey);
    }

    public string Generate(Guid applicationId, Guid eventId, Guid userId, DateTimeOffset issuedAt)
    {
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var payload = string.Join(Separator,
            Version,
            applicationId.ToString("N"),
            eventId.ToString("N"),
            userId.ToString("N"),
            issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            nonce);

        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Base64UrlEncode(Sign(encodedPayload));
        return $"{encodedPayload}.{signature}";
    }

    public bool TryValidate(string token, out QrTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
            return false;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return false;

        if (!TryBase64UrlDecode(parts[1], out var providedSignature))
            return false;

        var expectedSignature = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
            return false;

        if (!TryBase64UrlDecode(parts[0], out var payloadBytes))
            return false;

        var fields = Encoding.UTF8.GetString(payloadBytes).Split(Separator);
        if (fields.Length != 6 || fields[0] != Version)
            return false;

        if (!Guid.TryParseExact(fields[1], "N", out var applicationId) ||
            !Guid.TryParseExact(fields[2], "N", out var eventId) ||
            !Guid.TryParseExact(fields[3], "N", out var userId) ||
            !long.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var issuedAtSeconds))
            return false;

        payload = new QrTokenPayload(applicationId, eventId, userId, DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds));
        return true;
    }

    private byte[] Sign(string encodedPayload) =>
        HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(encodedPayload));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value))
            return false;

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            0 => base64,
            _ => string.Empty
        };

        if (base64.Length == 0)
            return false;

        var buffer = new byte[base64.Length * 3 / 4];
        if (!Convert.TryFromBase64String(base64, buffer, out var written))
            return false;

        bytes = buffer[..written];
        return true;
    }
}
