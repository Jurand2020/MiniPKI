using MiniPKI.Core.Domain;

namespace MiniPKI.Core.Services;

/// <summary>
/// Append-only audit logging to files.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string details, string? actor = null);
    Task<List<AuditEntry>> GetEntriesAsync(int limit = 100);
}
