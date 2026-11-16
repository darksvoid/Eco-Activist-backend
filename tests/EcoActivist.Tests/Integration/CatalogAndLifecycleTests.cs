using System.Net;
using System.Net.Http.Json;
using EcoActivist.Application.Articles;
using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Events;
using EcoActivist.Application.Lifecycle;
using EcoActivist.Domain.Enums;
using EcoActivist.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcoActivist.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class CatalogAndLifecycleTests : IntegrationTestBase
{
    public CatalogAndLifecycleTests(IntegrationTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Catalog_SortByDistance_ReturnsNearestFirstWithDistanceInMeters()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var far = await CreatePublishedEventAsync(organizer, latitude: 59.9343, longitude: 30.3351, title: $"Far {marker}");   // Saint Petersburg
        var near = await CreatePublishedEventAsync(organizer, latitude: 55.7520, longitude: 37.6175, title: $"Near {marker}"); // Moscow centre
        var category = await CreatePublishedEventAsync(organizer, categoryId: CategoryConfiguration.TreePlantingCategoryId, title: $"Trees {marker}");

        using var client = CreateClient();
        var result = await ReadAsync<PagedResult<EventListItemDto>>(await client.GetAsync(
            $"/api/v1/events?search={marker}&sortBy=Distance&userLat=55.7558&userLon=37.6173&categoryId={CategoryConfiguration.CleanupCategoryId}"));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([near, far], result.Items.Select(i => i.Id).ToArray());
        Assert.DoesNotContain(result.Items, i => i.Id == category);

        var nearDistance = result.Items[0].DistanceMeters!.Value;
        var farDistance = result.Items[1].DistanceMeters!.Value;
        Assert.InRange(nearDistance, 300, 600);
        Assert.InRange(farDistance, 600_000, 700_000);
        Assert.Equal(55.7520, result.Items[0].Location.Latitude, 4);
    }

    [Fact]
    public async Task Catalog_SortByAvailablePlaces_AndPagination()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var volunteer = await CreateVolunteerAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var small = await CreatePublishedEventAsync(organizer, capacity: 2, title: $"Small {marker}");
        var big = await CreatePublishedEventAsync(organizer, capacity: 20, title: $"Big {marker}");
        await ApplyAsync(volunteer, small);

        using var client = CreateClient();
        var page = await ReadAsync<PagedResult<EventListItemDto>>(await client.GetAsync(
            $"/api/v1/events?search={marker}&sortBy=AvailablePlaces&sortDirection=Desc&page=1&pageSize=1"));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        var first = Assert.Single(page.Items);
        Assert.Equal(big, first.Id);
        Assert.Equal(20, first.AvailablePlaces);

        var second = await ReadAsync<PagedResult<EventListItemDto>>(await client.GetAsync(
            $"/api/v1/events?search={marker}&sortBy=AvailablePlaces&sortDirection=Desc&page=2&pageSize=1"));
        Assert.Equal(small, second.Items.Single().Id);
        Assert.Equal(1, second.Items.Single().AvailablePlaces);
    }

    [Fact]
    public async Task Catalog_SortByDistanceWithoutCoordinates_Returns400()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/events?sortBy=Distance");

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task LifecycleWorker_ExpiresPendingRemindsConfirmedAndMarksAbsent()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var pendingVolunteer = await CreateVolunteerAsync();
        var confirmedVolunteer = await CreateVolunteerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 5);

        var pending = await ApplyAsync(pendingVolunteer, eventId);
        var confirmed = await ApplyAsync(confirmedVolunteer, eventId);
        await ConfirmAsync(organizer, confirmed.Id);

        // Less than 24h before the start.
        Factory.Clock.Advance(TimeSpan.FromDays(2) + TimeSpan.FromHours(1));
        await RunLifecycleAsync();

        var pendingStatus = await Factory.ExecuteDbAsync(db =>
            db.EventApplications.Where(a => a.Id == pending.Id).Select(a => a.Status).SingleAsync());
        Assert.Equal(ApplicationStatus.Expired, pendingStatus);

        var reminders = await Factory.ExecuteDbAsync(db =>
            db.Notifications.CountAsync(n => n.UserId == confirmedVolunteer.Id && n.Title == "Напоминание о мероприятии"));
        Assert.Equal(1, reminders);

        // Idempotency: a second run must not duplicate reminders.
        await RunLifecycleAsync();
        reminders = await Factory.ExecuteDbAsync(db =>
            db.Notifications.CountAsync(n => n.UserId == confirmedVolunteer.Id && n.Title == "Напоминание о мероприятии"));
        Assert.Equal(1, reminders);

        // 48 hours after EndDate without attendance mark.
        Factory.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromHours(3) + TimeSpan.FromHours(49));
        await RunLifecycleAsync();

        var absence = await Factory.ExecuteDbAsync(db =>
            db.AttendanceRecords.Where(r => r.EventId == eventId && r.UserId == confirmedVolunteer.Id).Select(r => r.Status).SingleAsync());
        Assert.Equal(AttendanceStatus.Absent, absence);

        var eventStatus = await Factory.ExecuteDbAsync(db => db.Events.Where(e => e.Id == eventId).Select(e => e.Status).SingleAsync());
        Assert.Equal(EventStatus.Completed, eventStatus);
    }

    [Fact]
    public async Task Articles_AdminCrud_PublicSeesOnlyPublished()
    {
        var admin = await LoginAsync(IntegrationTestFactory.AdminEmail, IntegrationTestFactory.AdminPassword);
        using var adminClient = CreateClient(admin.AccessToken);
        using var anonymous = CreateClient();

        var create = await adminClient.PostAsJsonAsync("/api/v1/admin/articles",
            new { Title = "Как сортировать мусор", Content = "Пластик отдельно", Category = "Сортировка", IsPublished = false }, Json);
        await AssertStatusAsync(HttpStatusCode.Created, create);
        var article = await ReadAsync<ArticleDto>(create);

        await AssertStatusAsync(HttpStatusCode.NotFound, await anonymous.GetAsync($"/api/v1/articles/{article.Id}"));

        var update = await adminClient.PutAsJsonAsync($"/api/v1/admin/articles/{article.Id}",
            new { Title = "Как сортировать мусор", Content = "Пластик и стекло отдельно", Category = "Сортировка", IsPublished = true }, Json);
        await AssertStatusAsync(HttpStatusCode.OK, update);

        var published = await ReadAsync<ArticleDto>(await anonymous.GetAsync($"/api/v1/articles/{article.Id}"));
        Assert.Equal("Пластик и стекло отдельно", published.Content);

        var volunteer = await CreateVolunteerAsync();
        var forbidden = await volunteer.Client.DeleteAsync($"/api/v1/admin/articles/{article.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await AssertStatusAsync(HttpStatusCode.NoContent, await adminClient.DeleteAsync($"/api/v1/admin/articles/{article.Id}"));
        await AssertStatusAsync(HttpStatusCode.NotFound, await anonymous.GetAsync($"/api/v1/articles/{article.Id}"));
    }

    private async Task<EventLifecycleResult> RunLifecycleAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventLifecycleService>().ProcessAsync(CancellationToken.None);
    }
}
