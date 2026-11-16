using EcoActivist.Application.Admin;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Articles;
using EcoActivist.Application.Attendance;
using EcoActivist.Application.Auth;
using EcoActivist.Application.Categories;
using EcoActivist.Application.Events;
using EcoActivist.Application.Lifecycle;
using EcoActivist.Application.Notifications;
using EcoActivist.Application.Volunteers;
using Microsoft.Extensions.DependencyInjection;

namespace EcoActivist.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddScoped<AuthService>();
        services.AddScoped<EventCatalogService>();
        services.AddScoped<OrganizerEventService>();
        services.AddScoped<VolunteerApplicationService>();
        services.AddScoped<OrganizerApplicationService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<ArticleService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<VolunteerProfileService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<OrganizerModerationService>();
        services.AddScoped<EventLifecycleService>();

        return services;
    }
}
