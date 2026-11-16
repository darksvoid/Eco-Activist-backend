using EcoActivist.Application.Common.Interfaces;
using EcoActivist.Application.Common.Security;
using EcoActivist.Domain.Enums;
using EcoActivist.Infrastructure.BackgroundServices;
using EcoActivist.Infrastructure.Persistence;
using EcoActivist.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EcoActivist.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services);
        AddSecurity(services);
        AddBackgroundServices(services);

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);

        // Connection string is resolved lazily so that hosts/tests can override configuration after service registration.
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                                   ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddHostedService<DatabaseInitializer>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<QrCodeOptions>()
            .BindConfiguration(QrCodeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IQrTokenService, QrTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, timeProvider) =>
            {
                bearer.MapInboundClaims = false;
                bearer.RequireHttpsMetadata = false;
                bearer.TokenValidationParameters = JwtTokenService.CreateValidationParameters(jwt.Value, timeProvider);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireVolunteer, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.Role, nameof(UserRole.Volunteer)))
            .AddPolicy(AuthorizationPolicies.RequireApprovedOrganizer, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.Role, nameof(UserRole.Organizer))
                .RequireClaim(AppClaimTypes.OrganizerStatus, nameof(OrganizerStatus.Approved)))
            .AddPolicy(AuthorizationPolicies.RequireAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.Role, nameof(UserRole.Admin)));
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
        services.AddOptions<EventLifecycleWorkerOptions>()
            .BindConfiguration(EventLifecycleWorkerOptions.SectionName)
            .Validate(o => o.Interval > TimeSpan.Zero, "Worker interval must be positive.");

        services.AddHostedService<EventLifecycleWorker>();
    }
}
