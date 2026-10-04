using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Integration;

public class AuditServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly FileAuditService _audit;

    public AuditServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-audit-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _audit = new FileAuditService(_paths);
    }

    [Fact]
    public async Task LogAsync_CreatesAuditLogFile()
    {
        await _audit.LogAsync("TEST", "Test audit entry");

        var files = Directory.GetFiles(_paths.AuditPath, "audit-*.jsonl");
        Assert.Single(files);
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsLoggedEntries()
    {
        await _audit.LogAsync("ISSUE", "Issued cert");
        await Task.Delay(10); // Ensure different timestamps
        await _audit.LogAsync("REVOKE", "Revoked cert");

        var entries = await _audit.GetEntriesAsync();

        Assert.Equal(2, entries.Count);
        // Sorted by timestamp descending (newest first)
        Assert.Equal("REVOKE", entries[0].Action);
        Assert.Equal("ISSUE", entries[1].Action);
    }

    [Fact]
    public async Task GetEntriesAsync_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            await _audit.LogAsync("TEST", $"Entry {i}");
        }

        var entries = await _audit.GetEntriesAsync(limit: 3);

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsEmptyListWhenNoFiles()
    {
        var entries = await _audit.GetEntriesAsync();

        Assert.Empty(entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
