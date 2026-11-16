using System.Net;
using System.Net.Http.Json;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Volunteers;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class NegativeScenarioTests : IntegrationTestBase
{
    public NegativeScenarioTests(IntegrationTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task QrScan_Repeated_ReturnsConflictAndDoesNotAwardPointsTwice()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(10));

        var first = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, first);

        var second = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);
        await AssertStatusAsync(HttpStatusCode.Conflict, second);

        var manual = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/manual", new { UserId = volunteer.Id }, Json);
        await AssertStatusAsync(HttpStatusCode.Conflict, manual);

        var profile = await ReadAsync<VolunteerProfileDto>(await volunteer.Client.GetAsync("/api/v1/volunteer/profile"));
        Assert.Equal(50, profile.EcoPointsBalance);
        Assert.Single(profile.Transactions.Items);

        var records = await Factory.ExecuteDbAsync(db => db.AttendanceRecords.CountAsync(r => r.EventId == eventId && r.UserId == volunteer.Id));
        Assert.Equal(1, records);
    }

    [Fact]
    public async Task QrScan_ConcurrentDuplicates_AwardPointsExactlyOnce()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(10));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var balance = await Factory.ExecuteDbAsync(db => db.Users.Where(u => u.Id == volunteer.Id).Select(u => u.EcoPointsBalance).SingleAsync());
        Assert.Equal(50, balance);
    }

    [Fact]
    public async Task CancelApplication_AfterEventStarted_IsRejected()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromSeconds(1));

        var response = await volunteer.Client.PostAsync($"/api/v1/applications/{application.Id}/cancel", null);
        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, response);

        var status = await Factory.ExecuteDbAsync(db => db.EventApplications.Where(a => a.Id == application.Id).Select(a => a.Status).SingleAsync());
        Assert.Equal(ApplicationStatus.Confirmed, status);
    }

    [Fact]
    public async Task QrScan_BeforeEventStart_IsRejected()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        var response = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);

        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, response);
    }

    [Fact]
    public async Task QrScan_AfterAttendanceWindowClosed_IsRejected()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        // Start (+3d) + duration (3h) + grace period (48h) + 1 minute.
        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromHours(3) + TimeSpan.FromHours(48) + TimeSpan.FromMinutes(1));

        var response = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);

        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, response);
    }

    [Fact]
    public async Task QrScan_ByAnotherOrganizer_IsForbidden()
    {
        var owner = await CreateApprovedOrganizerAsync();
        var stranger = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(owner);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(owner, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(5));

        var response = await stranger.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
    }

    [Fact]
    public async Task QrScan_TokenOfAnotherEvent_IsRejected()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var firstEvent = await CreatePublishedEventAsync(organizer);
        var secondEvent = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, firstEvent);
        await ConfirmAsync(organizer, application.Id);
        var qr = await GetQrAsync(volunteer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(5));

        var response = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{secondEvent}/attendance/qr", new { qr.Token }, Json);
        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, response);

        var forged = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{firstEvent}/attendance/qr",
            new { Token = qr.Token[..^2] + (qr.Token[^2..] == "AA" ? "BB" : "AA") }, Json);
        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, forged);
    }

    [Fact]
    public async Task OrganizerEndpoints_PendingOrganizer_AreForbidden()
    {
        var pending = await RegisterAsync(UserRole.Organizer);
        using var client = CreateClient(pending.AccessToken);

        var response = await client.GetAsync("/api/v1/organizer/events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_AdminRole_IsRejected()
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"hacker-{Guid.NewGuid():N}@ecoactivist.test",
            Phone = "+79990000000",
            Password = DefaultPassword,
            Role = UserRole.Admin
        }, Json);

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var volunteer = await RegisterAsync(UserRole.Volunteer);
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { volunteer.Email, Password = "wrong-password" }, Json);

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task Apply_WithinTwentyFourHoursOfStart_IsRejected()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, date: Now.AddHours(20));

        var response = await volunteer.Client.PostAsync($"/api/v1/events/{eventId}/apply", null);

        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, response);
    }

    [Fact]
    public async Task Qr_ForAnotherUsersApplication_IsNotFound()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var owner = await CreateVolunteerAsync();
        var intruder = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(owner, eventId);
        await ConfirmAsync(organizer, application.Id);

        var response = await intruder.Client.GetAsync($"/api/v1/applications/{application.Id}/qr");

        await AssertStatusAsync(HttpStatusCode.NotFound, response);
    }
}
