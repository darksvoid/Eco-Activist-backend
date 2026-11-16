using EcoActivist.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace EcoActivist.Tests.Infrastructure;

public class QrTokenServiceTests
{
    private static QrTokenService CreateService(string key = "test-qr-signing-key-0123456789-abcdef") =>
        new(Options.Create(new QrCodeOptions { SigningKey = key }));

    [Fact]
    public void Generate_ThenValidate_ReturnsOriginalPayload()
    {
        var service = CreateService();
        var applicationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var token = service.Generate(applicationId, eventId, userId, issuedAt);

        Assert.True(service.TryValidate(token, out var payload));
        Assert.NotNull(payload);
        Assert.Equal(applicationId, payload.ApplicationId);
        Assert.Equal(eventId, payload.EventId);
        Assert.Equal(userId, payload.UserId);
        Assert.Equal(issuedAt, payload.IssuedAt);
    }

    [Fact]
    public void Generate_SameInput_ProducesUniqueTokens()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.NotEqual(service.Generate(id, id, id, now), service.Generate(id, id, id, now));
    }

    [Fact]
    public void Validate_TamperedPayload_Fails()
    {
        var service = CreateService();
        var token = service.Generate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var forged = CreateService().Generate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var tampered = forged.Split('.')[0] + "." + token.Split('.')[1];

        Assert.False(service.TryValidate(tampered, out _));
    }

    [Fact]
    public void Validate_TokenSignedWithAnotherKey_Fails()
    {
        var token = CreateService("another-signing-key-0123456789-abcdefgh")
            .Generate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(CreateService().TryValidate(token, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void Validate_MalformedToken_Fails(string token)
    {
        Assert.False(CreateService().TryValidate(token, out _));
    }
}
