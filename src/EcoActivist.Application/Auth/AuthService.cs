using System.ComponentModel.DataAnnotations;
using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcoActivist.Application.Auth;

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(User.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required, Phone, MaxLength(User.PhoneMaxLength)]
    public string Phone { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public UserRole Role { get; init; } = UserRole.Volunteer;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(User.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    UserRole Role,
    OrganizerStatus? OrganizerStatus,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType = "Bearer");

public sealed class AuthService
{
    private static readonly Lazy<string> DummyPasswordHashSeed = new(() => Guid.NewGuid().ToString("N"));
    private static string? _dummyPasswordHash;

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role is not (UserRole.Volunteer or UserRole.Organizer))
            throw new RequestValidationException(nameof(request.Role), "Самостоятельная регистрация доступна только для ролей Volunteer и Organizer.");

        var email = User.NormalizeEmail(request.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Пользователь с таким email уже зарегистрирован.");

        var user = User.Create(email, request.Phone, _passwordHasher.Hash(request.Password), request.Role, _timeProvider.GetUtcNow());
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Зарегистрирован пользователь {UserId} с ролью {Role}", user.Id, user.Role);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            _passwordHasher.Verify(GetDummyPasswordHash(), request.Password);
            throw new AuthenticationFailedException("Неверный email или пароль.");
        }

        var outcome = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (outcome == PasswordVerificationOutcome.Failed)
            throw new AuthenticationFailedException("Неверный email или пароль.");

        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(_passwordHasher.Hash(request.Password));
            await _db.SaveChangesAsync(ct);
        }

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = _jwtTokenService.CreateAccessToken(user);
        return new AuthResponse(user.Id, user.Email, user.Role, user.Status, token.Token, token.ExpiresAt);
    }

    private string GetDummyPasswordHash() =>
        _dummyPasswordHash ??= _passwordHasher.Hash(DummyPasswordHashSeed.Value);
}
