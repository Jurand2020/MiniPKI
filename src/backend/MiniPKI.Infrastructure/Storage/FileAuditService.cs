using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPKI.Core.Domain;
using MiniPKI.Core.Services;

namespace MiniPKI.Infrastructure.Storage;

/// <summary>
/// JSON-serializable audit entry persisted in daily audit log files.
/// </summary>
public class StoredAuditEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }
}

/// <summary>
/// Append-only audit log service. Writes JSON-lines to daily files.
/// </summary>
public class FileAuditService : IAuditService
{
    private readonly DataPathProvider _paths;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileAuditService(DataPathProvider paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Appends an audit entry to today's audit log file.
    /// </summary>
    public async Task LogAsync(string action, string details, string? actor = null)
    {
        var entry = new StoredAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Details = details,
            Actor = actor
        };

        await _lock.WaitAsync();
        try
        {
            var fileName = $"audit-{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
            var filePath = Path.Combine(_paths.AuditPath, fileName);
            var json = JsonSerializer.Serialize(entry, AuditJsonContext.Default.StoredAuditEntry);
            await File.AppendAllTextAsync(filePath, json + "\n");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reads all audit entries from all audit log files.
    /// Returns entries sorted by timestamp descending (newest first).
    /// </summary>
    public async Task<List<AuditEntry>> GetEntriesAsync(int limit = 100)
    {
        var entries = new List<AuditEntry>();
        if (!Directory.Exists(_paths.AuditPath)) return entries;

        var files = Directory.GetFiles(_paths.AuditPath, "audit-*.jsonl")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var stored = JsonSerializer.Deserialize(line, AuditJsonContext.Default.StoredAuditEntry);
                if (stored == null) continue;
                entries.Add(new AuditEntry
                {
                    Timestamp = stored.Timestamp,
                    Action = stored.Action,
                    Details = stored.Details,
                    Actor = stored.Actor
                });
            }
        }

        return entries
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();
    }
}
