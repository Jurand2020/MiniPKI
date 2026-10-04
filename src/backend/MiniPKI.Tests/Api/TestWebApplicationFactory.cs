using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniPKI.Api.Auth;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Tests.Api;

/// <summary>
/// Custom WebApplicationFactory that configures test-specific services:
/// - Uses a temporary data directory
/// - Sets a known password hash for authentication tests
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string TempDataPath { get; } = Path.Combine(Path.GetTempPath(), "minipki-api-test-" + Guid.NewGuid().ToString("N")[..8]);

    // Password: "test123" — pre-computed PBKDF2 hash
    public const string TestPasswordHash = "pbkdf2-sha256:100000:c2FsdA==:JQdAIOhXL8/MW6cbncDnGU6CCTDlEkQq0uJo+whXxV0=";
    public const string TestPassword = "test123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataPath"] = TempDataPath,
                ["Auth:PasswordHash"] = TestPasswordHash,
                ["Auth:SessionTimeoutMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Ensure directories exist
            var paths = new DataPathProvider(TempDataPath);
            paths.EnsureDirectoriesExist();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        return base.CreateHost(builder);
    }
}
