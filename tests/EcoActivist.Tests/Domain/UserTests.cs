using EcoActivist.Domain.Entities;
using EcoActivist.Domain.Enums;

namespace EcoActivist.Tests.Domain;

public class UserTests
{
    [Fact]
    public void Create_ValidData_SetsProperties()
    {
        var email = "test@example.com";
        var phone = "1234567890";
        var role = UserRole.Volunteer;

        var user = User.Create(email, phone, role);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(phone, user.Phone);
        Assert.Equal(role, user.Role);
    }

    [Fact]
    public void UpdateContactInfo_ValidData_UpdatesProperties()
    {
        var user = User.Create("old@example.com", "000", UserRole.Volunteer);
        var newEmail = "new@example.com";
        var newPhone = "111";

        user.UpdateContactInfo(newEmail, newPhone);

        Assert.Equal(newEmail, user.Email);
        Assert.Equal(newPhone, user.Phone);
    }

    [Fact]
    public void ChangeRole_UpdatesRole()
    {
        var user = User.Create("test@example.com", "000", UserRole.Guest);

        user.ChangeRole(UserRole.Organizer);

        Assert.Equal(UserRole.Organizer, user.Role);
    }
}