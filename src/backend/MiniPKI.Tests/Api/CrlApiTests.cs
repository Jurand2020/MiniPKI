using System.Net;
using System.Net.Http.Json;
using MiniPKI.Api.Models;

namespace MiniPKI.Tests.Api;

public class CrlApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CrlApiTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetCurrentCrl_WhenUnauthenticated_ReturnsCrl()
    {
        // CRL endpoint should be public (no auth required)
        var response = await _client.GetAsync("/api/crl/current");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var crlPem = await response.Content.ReadAsStringAsync();
        Assert.Contains("-----BEGIN X509 CRL-----", crlPem);
    }

    [Fact]
    public async Task GenerateCrl_WhenAuthenticated_ReturnsCrl()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.PostAsync("/api/crl/generate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var crlPem = await response.Content.ReadAsStringAsync();
        Assert.Contains("-----BEGIN X509 CRL-----", crlPem);
    }

    [Fact]
    public async Task GenerateCrl_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/api/crl/generate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_factory.TempDataPath))
            Directory.Delete(_factory.TempDataPath, true);
    }
}
