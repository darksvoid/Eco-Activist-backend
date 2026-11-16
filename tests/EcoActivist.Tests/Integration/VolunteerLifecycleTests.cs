using System.Net;
using System.Net.Http.Json;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Attendance;
using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Events;
using EcoActivist.Application.Notifications;
using EcoActivist.Application.Volunteers;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EcoActivist.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class VolunteerLifecycleTests : IntegrationTestBase
{
    private const int CleanupReward = 50;

    public VolunteerLifecycleTests(IntegrationTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task FullCycle_RegisterApplyConfirmQrScan_AwardsEcoPoints()
    {
        // 1. Registration + login of a volunteer.
        var registered = await RegisterAsync(UserRole.Volunteer);
        Assert.Equal(UserRole.Volunteer, registered.Role);
        Assert.Null(registered.OrganizerStatus);

        var login = await LoginAsync(registered.Email);
        var volunteer = new TestUser(login.UserId, login.Email, CreateClient(login.AccessToken));

        // 2. Approved organizer publishes an event.
        var organizer = await CreateApprovedOrganizerAsync();
        var marker = Guid.NewGuid().ToString("N")[..10];
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 3, title: $"Субботник в парке {marker}");

        using var anonymous = CreateClient();
        var catalog = await ReadAsync<PagedResult<EventListItemDto>>(
            await anonymous.GetAsync($"/api/v1/events?search={Uri.EscapeDataString($"СУББОТНИК В ПАРКЕ {marker}")}"));
        var listed = Assert.Single(catalog.Items);
        Assert.Equal(eventId, listed.Id);
        Assert.Equal(3, listed.AvailablePlaces);
        Assert.Equal(CleanupReward, listed.EcoPointsReward);

        // 3. Volunteer applies: Pending, one place reserved.
        var application = await ApplyAsync(volunteer, eventId);
        Assert.Equal(ApplicationStatus.Pending, application.Status);

        var details = await ReadAsync<EventDetailsDto>(await anonymous.GetAsync($"/api/v1/events/{eventId}"));
        Assert.Equal(2, details.AvailablePlaces);

        // QR is not available until confirmation.
        var qrBeforeConfirm = await volunteer.Client.GetAsync($"/api/v1/applications/{application.Id}/qr");
        await AssertStatusAsync(HttpStatusCode.UnprocessableEntity, qrBeforeConfirm);

        // 4. Organizer sees and confirms the application.
        var organizerView = await ReadAsync<List<OrganizerApplicationDto>>(
            await organizer.Client.GetAsync($"/api/v1/organizer/events/{eventId}/applications"));
        Assert.Contains(organizerView, a => a.Id == application.Id && a.UserId == volunteer.Id);

        var confirmed = await ConfirmAsync(organizer, application.Id);
        Assert.Equal(ApplicationStatus.Confirmed, confirmed.Status);

        // 5. Volunteer obtains the signed QR token.
        var qr = await GetQrAsync(volunteer, application.Id);
        Assert.Equal(eventId, qr.EventId);
        Assert.False(string.IsNullOrWhiteSpace(qr.Token));

        // 6. Event starts, organizer scans the QR code.
        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(30));

        var scan = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/qr", new { qr.Token }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, scan);
        var attendance = await ReadAsync<AttendanceResultDto>(scan);

        Assert.Equal(AttendanceStatus.Attended, attendance.Status);
        Assert.Equal(volunteer.Id, attendance.UserId);
        Assert.Equal(CleanupReward, attendance.EcoPointsAwarded);
        Assert.Equal(CleanupReward, attendance.EcoPointsBalance);

        // 7. Balance, history and statistics in the volunteer profile.
        var profile = await ReadAsync<VolunteerProfileDto>(await volunteer.Client.GetAsync("/api/v1/volunteer/profile"));
        Assert.Equal(CleanupReward, profile.EcoPointsBalance);
        Assert.Equal(1, profile.Statistics.AttendedCount);
        Assert.Equal(CleanupReward, profile.Statistics.TotalEcoPointsEarned);
        var transaction = Assert.Single(profile.Transactions.Items);
        Assert.Equal(CleanupReward, transaction.Amount);
        Assert.Equal(attendance.AttendanceRecordId, transaction.AttendanceRecordId);

        var myApplications = await ReadAsync<List<MyApplicationDto>>(await volunteer.Client.GetAsync("/api/v1/applications/my"));
        var mine = Assert.Single(myApplications, a => a.Id == application.Id);
        Assert.Equal(AttendanceStatus.Attended, mine.AttendanceStatus);
        Assert.Equal(eventId, mine.Event.Id);

        var notifications = await ReadAsync<PagedResult<NotificationDto>>(await volunteer.Client.GetAsync("/api/v1/notifications"));
        Assert.Contains(notifications.Items, n => n.Title == "Заявка подтверждена");
        Assert.Contains(notifications.Items, n => n.Title == "Начислены ЭкоБаллы");

        var balanceInDb = await Factory.ExecuteDbAsync(db => db.Users.Where(u => u.Id == volunteer.Id).Select(u => u.EcoPointsBalance).SingleAsync());
        Assert.Equal(CleanupReward, balanceInDb);
    }

    [Fact]
    public async Task ManualAttendance_ConfirmedParticipant_AwardsEcoPoints()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer);
        var application = await ApplyAsync(volunteer, eventId);
        await ConfirmAsync(organizer, application.Id);

        Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromHours(4));

        var response = await organizer.Client.PostAsJsonAsync($"/api/v1/events/{eventId}/attendance/manual", new { UserId = volunteer.Id }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal(CleanupReward, (await ReadAsync<AttendanceResultDto>(response)).EcoPointsBalance);
    }

    [Fact]
    public async Task Cancel_BeforeStart_ReleasesPlace()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var first = await CreateVolunteerAsync();
        var second = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 1);

        var application = await ApplyAsync(first, eventId);
        await AssertStatusAsync(HttpStatusCode.Conflict, await second.Client.PostAsync($"/api/v1/events/{eventId}/apply", null));

        var cancel = await first.Client.PostAsync($"/api/v1/applications/{application.Id}/cancel", null);
        await AssertStatusAsync(HttpStatusCode.OK, cancel);
        Assert.Equal(ApplicationStatus.Cancelled, (await ReadAsync<ApplicationDto>(cancel)).Status);

        var secondApplication = await ApplyAsync(second, eventId);
        Assert.Equal(ApplicationStatus.Pending, secondApplication.Status);
    }

    [Fact]
    public async Task Reject_ByOrganizer_ReleasesPlaceAndStoresReason()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var first = await CreateVolunteerAsync();
        var second = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 1);
        var application = await ApplyAsync(first, eventId);

        var reject = await organizer.Client.PostAsJsonAsync(
            $"/api/v1/organizer/applications/{application.Id}/reject", new { Reason = "Нет опыта" }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, reject);
        var rejected = await ReadAsync<ApplicationDto>(reject);
        Assert.Equal(ApplicationStatus.Rejected, rejected.Status);
        Assert.Equal("Нет опыта", rejected.RejectionReason);

        await ApplyAsync(second, eventId);
    }
}
