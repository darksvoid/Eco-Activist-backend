using EcoActivist.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EcoActivist.Tests.Integration;

/// <summary>
/// Hosts the API against a real PostgreSQL + PostGIS database.
/// By default a disposable container is started via Testcontainers; set <c>ECOACTIVIST_TEST_POSTGRES</c>
/// to a PostGIS-enabled server connection string to use an existing server instead (a unique database is created and dropped).
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@ecoactivist.test";
    public const string AdminPassword = "Admin#Passw0rd-Integration";

    private const string DefaultImage = "postgis/postgis:17-3.5-alpine";

    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnectionString;

    public IntegrationTestFactory()
    {
        var external = Environment.GetEnvironmentVariable("ECOACTIVIST_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _externalConnectionString = new NpgsqlConnectionStringBuilder(external)
            {
                Database = $"ecoactivist_it_{Guid.NewGuid():N}"
            }.ConnectionString;
        }
        else
        {
            _container = new PostgreSqlBuilder(Environment.GetEnvironmentVariable("ECOACTIVIST_TEST_POSTGIS_IMAGE") ?? DefaultImage)
                .WithDatabase("ecoactivist_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }
    }

    public FakeTimeProvider Clock { get; } = new(TruncateToSeconds(DateTimeOffset.UtcNow));

    public string ConnectionString => _externalConnectionString ?? _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_container is not null)
            await _container.StartAsync();

        // Building the host applies migrations and seeds the administrator.
        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_externalConnectionString is not null)
        {
            await using (var scope = Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
            }
        }

        await base.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Workers:EventLifecycle:Enabled"] = "false",
            ["Seed:Admin:Email"] = AdminEmail,
            ["Seed:Admin:Password"] = AdminPassword,
            ["Seed:Admin:Phone"] = "+70000000000",
            ["Jwt:Issuer"] = "EcoActivist.Tests",
            ["Jwt:Audience"] = "EcoActivist.Tests",
            ["Jwt:SigningKey"] = "integration-tests-jwt-signing-key-0123456789",
            ["Jwt:AccessTokenLifetimeMinutes"] = (60 * 24 * 30).ToString(),
            ["QrCode:SigningKey"] = "integration-tests-qr-signing-key-0123456789"
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task<T> ExecuteDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationTestFactory>
{
    public const string Name = "Integration";
}
