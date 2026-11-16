using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;
using EcoActivist.Domain.Exceptions;

namespace EcoActivist.Tests.Domain;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Volunteer_SetsPropertiesWithoutOrganizerStatus()
    {
        var user = User.Create(" Test@Example.com ", "1234567890", "hash", UserRole.Volunteer, Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("1234567890", user.Phone);
        Assert.Equal(UserRole.Volunteer, user.Role);
        Assert.Null(user.Status);
        Assert.Equal(0, user.EcoPointsBalance);
    }

    [Fact]
    public void Create_Organizer_StartsInPendingStatus()
    {
        var user = User.Create("org@example.com", "000", "hash", UserRole.Organizer, Now);

        Assert.Equal(OrganizerStatus.Pending, user.Status);
        Assert.False(user.IsApprovedOrganizer);

        user.ApproveOrganizer();

        Assert.True(user.IsApprovedOrganizer);
    }

    [Fact]
    public void ApproveOrganizer_ForVolunteer_ThrowsDomainException()
    {
        var user = User.Create("v@example.com", "000", "hash", UserRole.Volunteer, Now);

        Assert.Throws<DomainException>(() => user.ApproveOrganizer());
    }

    [Fact]
    public void AddEcoPoints_PositiveAmount_IncreasesBalance()
    {
        var user = User.Create("v@example.com", "000", "hash", UserRole.Volunteer, Now);

        user.AddEcoPoints(50);
        user.AddEcoPoints(30);

        Assert.Equal(80, user.EcoPointsBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AddEcoPoints_NonPositiveAmount_Throws(int amount)
    {
        var user = User.Create("v@example.com", "000", "hash", UserRole.Volunteer, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => user.AddEcoPoints(amount));
    }

    [Fact]
    public void UpdateContactInfo_ValidData_UpdatesProperties()
    {
        var user = User.Create("old@example.com", "000", "hash", UserRole.Volunteer, Now);

        user.UpdateContactInfo("new@example.com", "111");

        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("111", user.Phone);
    }

    [Fact]
    public void ChangeRole_ToOrganizer_SetsPendingStatus()
    {
        var user = User.Create("test@example.com", "000", "hash", UserRole.Guest, Now);

        user.ChangeRole(UserRole.Organizer);

        Assert.Equal(UserRole.Organizer, user.Role);
        Assert.Equal(OrganizerStatus.Pending, user.Status);
    }
}
