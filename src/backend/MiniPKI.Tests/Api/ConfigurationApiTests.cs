using System.Net;
using System.Net.Http.Json;
using MiniPKI.Api.Models;

namespace MiniPKI.Tests.Api;

public class ConfigurationApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConfigurationApiTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetConfiguration_WhenAuthenticated_ReturnsConfig()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.GetAsync("/api/configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfiguration_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/configuration");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_WhenAuthenticated_UpdatesConfig()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var updateResponse = await _client.PutAsJsonAsync("/api/configuration",
            new UpdateConfigurationRequest
            {
                DefaultDomain = "updated.local",
                CrlUrl = "http://crl.updated.com/",
                DefaultValidityDays = 730,
                Algorithm = "RSA",
                RsaKeySize = 4096,
                Organization = "UpdatedOrg",
                OrganizationalUnit = "IT",
                Country = "US",
                State = "California",
                Locality = "San Francisco"
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_factory.TempDataPath))
            Directory.Delete(_factory.TempDataPath, true);
    }
}
