using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Auth;
using EcoActivist.Application.Events;
using EcoActivist.Domain.Enums;
using EcoActivist.Infrastructure.Persistence.Configurations;

namespace EcoActivist.Tests.Integration;

public abstract class IntegrationTestBase
{
    protected const string DefaultPassword = "Str0ng#Password";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected IntegrationTestBase(IntegrationTestFactory factory)
    {
        Factory = factory;
    }

    protected IntegrationTestFactory Factory { get; }

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    protected HttpClient CreateClient(string? accessToken = null)
    {
        var client = Factory.CreateClient();
        if (accessToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected async Task<AuthResponse> RegisterAsync(UserRole role, string? email = null)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@ecoactivist.test",
            Phone = "+79990001122",
            Password = DefaultPassword,
            Role = role
        }, Json);

        await AssertStatusAsync(HttpStatusCode.Created, response);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    protected async Task<AuthResponse> LoginAsync(string email, string password = DefaultPassword)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, response);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    protected async Task<TestUser> CreateVolunteerAsync()
    {
        var auth = await RegisterAsync(UserRole.Volunteer);
        return new TestUser(auth.UserId, auth.Email, CreateClient(auth.AccessToken));
    }

    protected async Task<TestUser> CreateApprovedOrganizerAsync()
    {
        var registered = await RegisterAsync(UserRole.Organizer);
        Assert.Equal(OrganizerStatus.Pending, registered.OrganizerStatus);

        var admin = await LoginAsync(IntegrationTestFactory.AdminEmail, IntegrationTestFactory.AdminPassword);
        using (var adminClient = CreateClient(admin.AccessToken))
        {
            var approve = await adminClient.PostAsync($"/api/v1/admin/organizers/{registered.UserId}/approve", null);
            await AssertStatusAsync(HttpStatusCode.OK, approve);
        }

        // Re-login to obtain a token carrying organizer_status=Approved.
        var auth = await LoginAsync(registered.Email);
        Assert.Equal(OrganizerStatus.Approved, auth.OrganizerStatus);
        return new TestUser(auth.UserId, auth.Email, CreateClient(auth.AccessToken));
    }

    protected async Task<Guid> CreatePublishedEventAsync(
        TestUser organizer,
        int capacity = 10,
        DateTimeOffset? date = null,
        double latitude = 55.7558,
        double longitude = 37.6173,
        Guid? categoryId = null,
        string? title = null)
    {
        var start = date ?? Now.AddDays(3);
        var create = await organizer.Client.PostAsJsonAsync("/api/v1/organizer/events", new CreateEventRequest
        {
            Title = title ?? $"Субботник {Guid.NewGuid():N}",
            Description = "Уборка парка",
            CategoryId = categoryId ?? CategoryConfiguration.CleanupCategoryId,
            Date = start,
            EndDate = start.AddHours(3),
            Latitude = latitude,
            Longitude = longitude,
            Address = "Москва, парк Горького",
            ParticipationConditions = "Перчатки выдаются на месте",
            Capacity = capacity
        }, Json);

        await AssertStatusAsync(HttpStatusCode.Created, create);
        var eventId = (await create.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var publish = await organizer.Client.PostAsync($"/api/v1/organizer/events/{eventId}/publish", null);
        await AssertStatusAsync(HttpStatusCode.OK, publish);

        return eventId;
    }

    protected async Task<ApplicationDto> ApplyAsync(TestUser volunteer, Guid eventId)
    {
        var response = await volunteer.Client.PostAsync($"/api/v1/events/{eventId}/apply", null);
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return (await response.Content.ReadFromJsonAsync<ApplicationDto>(Json))!;
    }

    protected async Task<ApplicationDto> ConfirmAsync(TestUser organizer, Guid applicationId)
    {
        var response = await organizer.Client.PostAsync($"/api/v1/organizer/applications/{applicationId}/confirm", null);
        await AssertStatusAsync(HttpStatusCode.OK, response);
        return (await response.Content.ReadFromJsonAsync<ApplicationDto>(Json))!;
    }

    protected async Task<QrCodeDto> GetQrAsync(TestUser volunteer, Guid applicationId)
    {
        var response = await volunteer.Client.GetAsync($"/api/v1/applications/{applicationId}/qr");
        await AssertStatusAsync(HttpStatusCode.OK, response);
        return (await response.Content.ReadFromJsonAsync<QrCodeDto>(Json))!;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    protected static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    protected sealed record TestUser(Guid Id, string Email, HttpClient Client);

    private sealed record IdResponse(Guid Id);
}
