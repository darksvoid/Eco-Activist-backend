using EcoActivist.Domain.Common;
using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using EcoActivist.Domain.Exceptions;

namespace EcoActivist.Tests.Domain;

public class EventTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private static Event CreateEvent(DateTimeOffset? date = null, int capacity = 10)
    {
        var start = date ?? Now.AddDays(3);
        return Event.Create(
            "Test Event",
            "Description",
            Guid.NewGuid(),
            start,
            start.AddHours(3),
            GeoLocation.CreatePoint(55.7558, 37.6173),
            "Moscow, Red Square",
            null,
            capacity,
            Guid.NewGuid());
    }

    [Fact]
    public void Create_ValidData_ReturnsEventInDraftStatus()
    {
        var evt = CreateEvent();

        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal("Test Event", evt.Title);
        Assert.Equal(EventStatus.Draft, evt.Status);
        Assert.Empty(evt.DomainEvents);
    }

    [Fact]
    public void Create_InvalidCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateEvent(capacity: 0));
    }

    [Fact]
    public void Create_EndDateNotAfterStart_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Event.Create(
            "Test", "Desc", Guid.NewGuid(), Now, Now, GeoLocation.CreatePoint(55, 37), "Addr", null, 10, Guid.NewGuid()));
    }

    [Fact]
    public void Create_NonUtcOffset_NormalizesDatesToUtc()
    {
        var local = new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.FromHours(3));

        var evt = CreateEvent(local);

        Assert.Equal(TimeSpan.Zero, evt.Date.Offset);
        Assert.Equal(local.UtcDateTime, evt.Date.UtcDateTime);
    }

    [Fact]
    public void Publish_DraftEvent_ChangesStatusAndAddsDomainEvent()
    {
        var evt = CreateEvent();

        evt.Publish();

        Assert.Equal(EventStatus.Published, evt.Status);
        Assert.Single(evt.DomainEvents);
        Assert.IsType<EventPublishedDomainEvent>(evt.DomainEvents.First());
    }

    [Fact]
    public void Publish_AlreadyPublishedEvent_ThrowsDomainException()
    {
        var evt = CreateEvent();
        evt.Publish();

        Assert.Throws<DomainException>(() => evt.Publish());
    }

    [Fact]
    public void Cancel_BeforeStart_ChangesStatusAndAddsDomainEvent()
    {
        var evt = CreateEvent();

        evt.Cancel("Reason", Now);

        Assert.Equal(EventStatus.Cancelled, evt.Status);
        Assert.IsType<EventCancelledDomainEvent>(Assert.Single(evt.DomainEvents));
    }

    [Fact]
    public void Cancel_AfterStart_ThrowsDomainException()
    {
        var evt = CreateEvent(Now.AddHours(-1));

        Assert.False(evt.CanBeCancelled(Now));
        Assert.Throws<DomainException>(() => evt.Cancel("Reason", Now));
    }

    [Fact]
    public void ApplicationCancellation_AllowedOnlyBeforeStart()
    {
        var evt = CreateEvent(Now.AddHours(1));

        Assert.True(evt.CanApplicationBeCancelled(Now));
        Assert.False(evt.CanApplicationBeCancelled(evt.Date));
        Assert.Throws<DomainException>(() => evt.EnsureApplicationCanBeCancelled(evt.Date.AddMinutes(1)));
    }

    [Fact]
    public void AttendanceWindow_FromStartUntil48HoursAfterEnd()
    {
        var evt = CreateEvent();
        evt.Publish();

        Assert.False(evt.IsAttendanceWindowOpen(evt.Date.AddSeconds(-1)));
        Assert.True(evt.IsAttendanceWindowOpen(evt.Date));
        Assert.True(evt.IsAttendanceWindowOpen(evt.EndDate.AddHours(48)));
        Assert.False(evt.IsAttendanceWindowOpen(evt.EndDate.AddHours(48).AddSeconds(1)));
        Assert.True(evt.IsAttendanceWindowClosed(evt.EndDate.AddHours(48).AddSeconds(1)));
    }

    [Fact]
    public void IsAcceptingApplications_ClosesTwentyFourHoursBeforeStart()
    {
        var evt = CreateEvent();
        evt.Publish();

        Assert.True(evt.IsAcceptingApplications(evt.Date.AddHours(-25)));
        Assert.False(evt.IsAcceptingApplications(evt.Date.AddHours(-24)));
    }

    [Fact]
    public void Complete_PublishedEvent_ChangesStatusToCompleted()
    {
        var evt = CreateEvent();
        evt.Publish();

        evt.Complete();

        Assert.Equal(EventStatus.Completed, evt.Status);
    }
}
