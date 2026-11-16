using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Events;
using EcoActivist.Domain.Exceptions;

namespace EcoActivist.Tests.Application;

public class EventApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private static EventApplication CreatePending()
    {
        var application = EventApplication.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        application.ClearDomainEvents();
        return application;
    }

    [Fact]
    public void Create_ValidData_SetsStatusToPendingAndAddsEvent()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var application = EventApplication.Create(userId, eventId, Now);

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(userId, application.UserId);
        Assert.Equal(eventId, application.EventId);
        Assert.Equal(ApplicationStatus.Pending, application.Status);
        Assert.True(application.IsActive);
        Assert.IsType<ApplicationSubmittedDomainEvent>(Assert.Single(application.DomainEvents));
    }

    [Fact]
    public void Confirm_PendingApplication_StoresQrTokenAndAddsEvent()
    {
        var application = CreatePending();

        application.Confirm("qr-token", Now);

        Assert.Equal(ApplicationStatus.Confirmed, application.Status);
        Assert.Equal("qr-token", application.QrCodeToken);
        Assert.Equal(Now, application.QrCodeGeneratedAt);
        Assert.True(application.MatchesQrToken("qr-token"));
        Assert.False(application.MatchesQrToken("other-token"));
        Assert.IsType<ApplicationConfirmedDomainEvent>(Assert.Single(application.DomainEvents));
    }

    [Fact]
    public void Confirm_NotPendingApplication_ThrowsDomainException()
    {
        var application = CreatePending();
        application.Confirm("qr-token", Now);

        Assert.Throws<DomainException>(() => application.Confirm("qr-token-2", Now));
    }

    [Fact]
    public void Reject_ConfirmedApplication_RevokesQrCode()
    {
        var application = CreatePending();
        application.Confirm("qr-token", Now);
        application.ClearDomainEvents();

        application.Reject("No spots left", Now);

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal("No spots left", application.RejectionReason);
        Assert.Null(application.QrCodeToken);
        Assert.False(application.IsActive);
        Assert.IsType<ApplicationRejectedDomainEvent>(Assert.Single(application.DomainEvents));
    }

    [Fact]
    public void Cancel_PendingApplication_ChangesStatusAndAddsEvent()
    {
        var application = CreatePending();

        application.Cancel(Now);

        Assert.Equal(ApplicationStatus.Cancelled, application.Status);
        Assert.IsType<ApplicationCancelledDomainEvent>(Assert.Single(application.DomainEvents));
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ThrowsDomainException()
    {
        var application = CreatePending();
        application.Cancel(Now);

        Assert.Throws<DomainException>(() => application.Cancel(Now));
    }

    [Fact]
    public void Expire_PendingApplication_ChangesStatus()
    {
        var application = CreatePending();

        application.Expire(Now);

        Assert.Equal(ApplicationStatus.Expired, application.Status);
        Assert.IsType<ApplicationExpiredDomainEvent>(Assert.Single(application.DomainEvents));
    }

    [Fact]
    public void Expire_ConfirmedApplication_ThrowsDomainException()
    {
        var application = CreatePending();
        application.Confirm("qr-token", Now);

        Assert.Throws<DomainException>(() => application.Expire(Now));
    }
}
