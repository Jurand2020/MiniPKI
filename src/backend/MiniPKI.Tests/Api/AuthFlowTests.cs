using System.Net;
using System.Net.Http.Json;
using MiniPKI.Api.Models;

namespace MiniPKI.Tests.Api;

public class AuthFlowTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/certificates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidPassword_ReturnsSuccess()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "wrongpassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthStatus_WhenNotAuthenticated_ReturnsFalse()
    {
        var response = await _client.GetAsync("/api/auth/status");
        var result = await response.Content.ReadFromJsonAsync<AuthStatusResponse>();
        Assert.False(result!.Authenticated);
    }

    [Fact]
    public async Task FullAuthFlow_Login_Access_Logout()
    {
        // Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Access protected endpoint
        var certResponse = await _client.GetAsync("/api/certificates");
        Assert.Equal(HttpStatusCode.OK, certResponse.StatusCode);

        // Logout
        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Access should now fail
        var postLogoutResponse = await _client.GetAsync("/api/certificates");
        Assert.Equal(HttpStatusCode.Unauthorized, postLogoutResponse.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_factory.TempDataPath))
            Directory.Delete(_factory.TempDataPath, true);
    }
}
