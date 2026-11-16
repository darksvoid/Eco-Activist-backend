using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcoActivist.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; set; }
}

public sealed class AdminSeedOptions
{
    public const string SectionName = "Seed:Admin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Phone { get; set; }
}

/// <summary>
/// Applies pending migrations (when enabled) and seeds the initial administrator account before the API starts serving requests.
/// </summary>
public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly IOptions<AdminSeedOptions> _adminOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<AdminSeedOptions> adminOptions,
        TimeProvider timeProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _databaseOptions = databaseOptions;
        _adminOptions = adminOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var admin = _adminOptions.Value;
        var seedAdmin = !string.IsNullOrWhiteSpace(admin.Email) && !string.IsNullOrWhiteSpace(admin.Password);

        if (!_databaseOptions.Value.ApplyMigrationsOnStartup && !seedAdmin)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (_databaseOptions.Value.ApplyMigrationsOnStartup)
        {
            _logger.LogInformation("Применение миграций базы данных");
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (seedAdmin)
        {
            var email = User.NormalizeEmail(admin.Email!);
            if (!await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                db.Users.Add(User.Create(
                    email,
                    string.IsNullOrWhiteSpace(admin.Phone) ? "-" : admin.Phone,
                    hasher.Hash(admin.Password!),
                    UserRole.Admin,
                    _timeProvider.GetUtcNow()));

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Создан администратор {Email}", email);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
