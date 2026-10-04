using System.Net;
using System.Net.Http.Json;
using MiniPKI.Api.Models;

namespace MiniPKI.Tests.Api;

public class AuditApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuditApiTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetAuditLog_WhenAuthenticated_ReturnsEntries()
    {
        // Login to generate audit entry
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.GetAsync("/api/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<AuditEntryResponse>>();
        Assert.NotNull(entries);
        Assert.NotEmpty(entries!);
    }

    [Fact]
    public async Task GetAuditLog_ReturnsEntriesSortedByTimeDescending()
    {
        // Login to generate audit entries
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });
        await Task.Delay(10);
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });
        await Task.Delay(10);
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.GetAsync("/api/audit");
        var entries = await response.Content.ReadFromJsonAsync<List<AuditEntryResponse>>();

        Assert.NotNull(entries);
        Assert.True(entries!.Count >= 3);

        // Verify entries are sorted by timestamp descending (newest first)
        for (var i = 1; i < entries.Count; i++)
        {
            Assert.True(entries[i - 1].Timestamp >= entries[i].Timestamp,
                $"Entry {i - 1} timestamp {entries[i - 1].Timestamp} should be >= entry {i} timestamp {entries[i].Timestamp}");
        }
    }

    [Fact]
    public async Task GetAuditLog_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_factory.TempDataPath))
            Directory.Delete(_factory.TempDataPath, true);
    }
}
