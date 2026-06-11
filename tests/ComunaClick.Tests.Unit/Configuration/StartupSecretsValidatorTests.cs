using ComunaClick.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ComunaClick.Tests.Unit.Configuration;

public sealed class StartupSecretsValidatorTests
{
    private const string StrongJwt = "super-strong-jwt-signing-key-0123456789-abc";
    private const string StrongWebhook = "super-strong-internal-webhook-key-0123456789";

    // H10: en dev la app arranca aunque haya placeholders.
    [Fact]
    public void Development_WithPlaceholders_DoesNotThrow()
    {
        var config = Build(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "CHANGE_ME",
            ["Payments:InternalWebhookKey"] = "CHANGE_ME"
        });

        StartupSecretsValidator.ValidateOrThrow(config, new FakeEnvironment("Development"));
    }

    // H10: en producción con placeholders, la app no arranca.
    [Fact]
    public void Production_WithPlaceholderSecrets_Throws()
    {
        var config = Build(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "CHANGE_ME",
            ["Payments:InternalWebhookKey"] = "CHANGE_ME"
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupSecretsValidator.ValidateOrThrow(config, new FakeEnvironment("Production")));
        Assert.Contains("Jwt:SigningKey", ex.Message);
        Assert.Contains("InternalWebhookKey", ex.Message);
    }

    // H10: en producción con secretos fuertes, arranca.
    [Fact]
    public void Production_WithStrongSecrets_DoesNotThrow()
    {
        var config = Build(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = StrongJwt,
            ["Payments:InternalWebhookKey"] = StrongWebhook,
            ["Marketplace:MercadoPago:ClientId"] = "client-id",
            ["Marketplace:MercadoPago:ClientSecret"] = "client-secret",
            ["Marketplace:MercadoPago:WebhookSecret"] = "webhook-secret",
            ["Marketplace:MercadoPago:EncryptionKey"] = "encryption-key"
        });

        StartupSecretsValidator.ValidateOrThrow(config, new FakeEnvironment("Production"));
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public FakeEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "ComunaClick.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
