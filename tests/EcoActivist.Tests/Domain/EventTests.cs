using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using NetTopologySuite.Geometries;
using Xunit;

namespace EcoActivist.Tests.Domain;

public class EventTests
{
    private readonly Point _defaultLocation = new Point(37.6173, 55.7558);

    [Fact]
    public void Create_ValidData_ReturnsEventInDraftStatus()
    {
        var organizerId = Guid.NewGuid();

        var evt = Event.Create("Test Event", "Description", DateTimeOffset.UtcNow, _defaultLocation, 100, organizerId);

        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal("Test Event", evt.Title);
        Assert.Equal(EventStatus.Draft, evt.Status);
        Assert.Empty(evt.DomainEvents);
    }

    [Fact]
    public void Create_InvalidCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Event.Create(
            "Test", "Desc", DateTimeOffset.UtcNow, _defaultLocation, 0, Guid.NewGuid()));
    }

    [Fact]
    public void Publish_DraftEvent_ChangesStatusAndAddsDomainEvent()
    {
        var evt = Event.Create("Test", "Desc", DateTimeOffset.UtcNow, _defaultLocation, 10, Guid.NewGuid());

        evt.Publish();

        Assert.Equal(EventStatus.Published, evt.Status);
        Assert.Single(evt.DomainEvents);
        Assert.IsType<EventPublishedDomainEvent>(evt.DomainEvents.First());
    }

    [Fact]
    public void Publish_AlreadyPublishedEvent_ThrowsInvalidOperationException()
    {
        var evt = Event.Create("Test", "Desc", DateTimeOffset.UtcNow, _defaultLocation, 10, Guid.NewGuid());
        evt.Publish();
        evt.ClearDomainEvents();

        Assert.Throws<InvalidOperationException>(() => evt.Publish());
    }

    [Fact]
    public void Cancel_DraftEvent_ChangesStatusAndAddsDomainEvent()
    {
        var evt = Event.Create("Test", "Desc", DateTimeOffset.UtcNow, _defaultLocation, 10, Guid.NewGuid());

        evt.Cancel("Reason");

        Assert.Equal(EventStatus.Cancelled, evt.Status);
        Assert.Single(evt.DomainEvents);
        Assert.IsType<EventCancelledDomainEvent>(evt.DomainEvents.First());
    }

    [Fact]
    public void Complete_PublishedEvent_ChangesStatusToCompleted()
    {
        var evt = Event.Create("Test", "Desc", DateTimeOffset.UtcNow, _defaultLocation, 10, Guid.NewGuid());
        evt.Publish();

        evt.Complete();

        Assert.Equal(EventStatus.Completed, evt.Status);
    }
}