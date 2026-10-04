using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPKI.Core.Domain;

namespace MiniPKI.Infrastructure.Storage;

/// <summary>
/// JSON-serializable metadata record persisted alongside certificate/key PEM files.
/// </summary>
public class StoredMetadata
{
    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("commonName")]
    public string CommonName { get; set; } = string.Empty;

    [JsonPropertyName("sanEntries")]
    public List<string> SanEntries { get; set; } = new();

    [JsonPropertyName("keyAlgorithm")]
    public KeyAlgorithm KeyAlgorithm { get; set; }

    [JsonPropertyName("keySize")]
    public int KeySize { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    [JsonPropertyName("revocationReason")]
    public string? RevocationReason { get; set; }
}

/// <summary>
/// Source-generated JSON context for storage serialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StoredMetadata))]
[JsonSerializable(typeof(List<StoredMetadata>))]
[JsonSerializable(typeof(StoredConfiguration))]
[JsonSerializable(typeof(StoredAuditEntry))]
[JsonSerializable(typeof(List<StoredAuditEntry>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class StorageJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Source-generated JSON context for audit log entries.
/// Uses non-indented serialization so each entry fits on a single line (JSON-lines format).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(StoredAuditEntry))]
[JsonSerializable(typeof(List<StoredAuditEntry>))]
public partial class AuditJsonContext : JsonSerializerContext
{
}
