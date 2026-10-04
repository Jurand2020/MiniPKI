namespace MiniPKI.Core.Domain;

/// <summary>
/// A single audit log entry. Stored in append-only files.
/// </summary>
public class AuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? Actor { get; set; }
}
