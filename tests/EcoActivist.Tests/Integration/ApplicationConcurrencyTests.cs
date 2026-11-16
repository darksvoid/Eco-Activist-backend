using System.Net;
using EcoActivist.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EcoActivist.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class ApplicationConcurrencyTests : IntegrationTestBase
{
    public ApplicationConcurrencyTests(IntegrationTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Apply_ConcurrentRequestsForLastPlace_ExactlyOneSucceeds()
    {
        const int competitors = 12;
        var organizer = await CreateApprovedOrganizerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 1);
        var volunteers = new List<TestUser>();
        for (var i = 0; i < competitors; i++)
            volunteers.Add(await CreateVolunteerAsync());

        using var start = new ManualResetEventSlim(false);
        var requests = volunteers
            .Select(v => Task.Run(async () =>
            {
                start.Wait();
                return await v.Client.PostAsync($"/api/v1/events/{eventId}/apply", null);
            }))
            .ToList();

        start.Set();
        var responses = await Task.WhenAll(requests);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(competitors - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var activeApplications = await Factory.ExecuteDbAsync(db => db.EventApplications
            .CountAsync(a => a.EventId == eventId && (a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Confirmed)));
        Assert.Equal(1, activeApplications);

        using var anonymous = CreateClient();
        var details = await ReadAsync<EcoActivist.Application.Events.EventDetailsDto>(
            await anonymous.GetAsync($"/api/v1/events/{eventId}"));
        Assert.Equal(0, details.AvailablePlaces);
    }

    [Fact]
    public async Task Apply_EventRowLockedByAnotherTransaction_WaitsAndSeesCommittedState()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 1);
        var competitor = await CreateVolunteerAsync();
        var volunteer = await CreateVolunteerAsync();

        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var lockCommand = new NpgsqlCommand("""SELECT 1 FROM "Events" WHERE "Id" = @id FOR UPDATE""", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("id", eventId);
            await lockCommand.ExecuteScalarAsync();
        }

        var applyTask = volunteer.Client.PostAsync($"/api/v1/events/{eventId}/apply", null);

        // The API request must block on SELECT ... FOR UPDATE while the row lock is held.
        var finishedFirst = await Task.WhenAny(applyTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(applyTask, finishedFirst);

        // The lock holder takes the last place and commits.
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "EventApplications" ("Id", "UserId", "EventId", "Status", "CreatedAt")
            VALUES (@id, @userId, @eventId, 'Pending', @createdAt)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("userId", competitor.Id);
            insert.Parameters.AddWithValue("eventId", eventId);
            insert.Parameters.AddWithValue("createdAt", Now);
            await insert.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        var response = await applyTask.WaitAsync(TimeSpan.FromSeconds(30));
        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    [Fact]
    public async Task Apply_SameVolunteerTwiceConcurrently_CreatesSingleActiveApplication()
    {
        var organizer = await CreateApprovedOrganizerAsync();
        var eventId = await CreatePublishedEventAsync(organizer, capacity: 5);
        var volunteer = await CreateVolunteerAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => volunteer.Client.PostAsync($"/api/v1/events/{eventId}/apply", null)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }
}
