using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;

namespace EcoActivist.Tests.Application;

public class EventApplicationTests
{
    [Fact]
    public void Create_ValidData_SetsStatusToPendingAndAddsEvent()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var application = EventApplication.Create(userId, eventId);

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(userId, application.UserId);
        Assert.Equal(eventId, application.EventId);
        Assert.Equal(ApplicationStatus.Pending, application.Status);
        Assert.Single(application.DomainEvents);
        Assert.IsType<ApplicationSubmittedDomainEvent>(application.DomainEvents.First());
    }

    [Fact]
    public void Confirm_PendingApplication_ChangesStatusAndAddsEvent()
    {
        var application = EventApplication.Create(Guid.NewGuid(), Guid.NewGuid());
        application.ClearDomainEvents();

        application.Confirm();

        Assert.Equal(ApplicationStatus.Confirmed, application.Status);
        Assert.Single(application.DomainEvents);
        Assert.IsType<ApplicationConfirmedDomainEvent>(application.DomainEvents.First());
    }

    [Fact]
    public void Confirm_NotPendingApplication_ThrowsInvalidOperationException()
    {
        var application = EventApplication.Create(Guid.NewGuid(), Guid.NewGuid());
        application.Confirm();
        application.ClearDomainEvents();

        Assert.Throws<InvalidOperationException>(() => application.Confirm());
    }

    [Fact]
    public void Reject_PendingApplication_ChangesStatusAndAddsEvent()
    {
        var application = EventApplication.Create(Guid.NewGuid(), Guid.NewGuid());
        application.ClearDomainEvents();

        application.Reject("No spots left");

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Single(application.DomainEvents);
        Assert.IsType<ApplicationRejectedDomainEvent>(application.DomainEvents.First());
    }

    [Fact]
    public void Cancel_PendingApplication_ChangesStatusAndAddsEvent()
    {
        var application = EventApplication.Create(Guid.NewGuid(), Guid.NewGuid());
        application.ClearDomainEvents();

        application.Cancel();

        Assert.Equal(ApplicationStatus.Cancelled, application.Status);
        Assert.Single(application.DomainEvents);
        Assert.IsType<ApplicationCancelledDomainEvent>(application.DomainEvents.First());
    }
}