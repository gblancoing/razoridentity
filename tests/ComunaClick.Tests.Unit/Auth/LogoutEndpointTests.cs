using ComunaClick.Acl.Controllers;
using ComunaClick.Acl.Contracts.Auth;
using ComunaClick.Acl.Domain;
using ComunaClick.Acl.Persistence;
using ComunaClick.Acl.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ComunaClick.Tests.Unit.Auth;

public sealed class LogoutEndpointTests
{
    private const string Pepper = "unit-test-refresh-pepper";

    [Fact]
    public async Task Logout_RevokesMatchingRefreshToken()
    {
        await using var db = CreateDb();
        const string raw = "raw-refresh-token-value";
        var token = SeedRefreshToken(db, raw);

        var controller = CreateController(db);
        var result = await controller.Logout(new LogoutRequest(raw));

        Assert.IsType<OkResult>(result);
        var stored = await db.RefreshTokens.SingleAsync(x => x.Id == token.Id);
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task Logout_UnknownToken_ReturnsOkWithoutError()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Logout(new LogoutRequest("does-not-exist"));

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Logout_EmptyToken_ReturnsOk()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Logout(new LogoutRequest(string.Empty));

        Assert.IsType<OkResult>(result);
    }

    private static AclDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AclDbContext>()
            .UseInMemoryDatabase($"acl-tests-{Guid.NewGuid():N}")
            .Options;
        return new AclDbContext(options);
    }

    private static RefreshToken SeedRefreshToken(AclDbContext db, string raw)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "logout@test.cl",
            DisplayName = "Logout Test",
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = TokenHasher.Hash(raw, Pepper),
            IssuedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        db.Users.Add(user);
        db.RefreshTokens.Add(token);
        db.SaveChanges();
        return token;
    }

    private static AuthController CreateController(AclDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenPepper"] = Pepper
            })
            .Build();

        // Logout solo usa el DbContext y la config; el resto de dependencias no se ejercitan.
        return new AuthController(
            db,
            tokenService: null!,
            configuration: config,
            recaptchaVerifier: null!,
            validators: Array.Empty<IExternalTokenValidator>(),
            logger: NullLogger<AuthController>.Instance);
    }
}
