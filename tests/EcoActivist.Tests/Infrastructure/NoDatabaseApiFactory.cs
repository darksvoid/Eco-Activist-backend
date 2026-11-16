using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EcoActivist.Tests.Infrastructure;

/// <summary>
/// API host for smoke tests that do not touch the database: migrations, seeding and background workers are disabled.
/// </summary>
public sealed class NoDatabaseApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["Workers:EventLifecycle:Enabled"] = "false",
            ["Seed:Admin:Email"] = "",
            ["Seed:Admin:Password"] = "",
            ["Jwt:Issuer"] = "EcoActivist.Tests",
            ["Jwt:Audience"] = "EcoActivist.Tests",
            ["Jwt:SigningKey"] = "smoke-tests-jwt-signing-key-0123456789-abcdef",
            ["QrCode:SigningKey"] = "smoke-tests-qr-signing-key-0123456789-abcdef"
        }));
    }
}
