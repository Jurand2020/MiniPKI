namespace MiniPKI.Core.Domain;

/// <summary>
/// Represents an issued TLS certificate with its metadata.
/// </summary>
public class CertificateRecord
{
    public string SerialNumber { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public List<string> SanEntries { get; set; } = new();
    public KeyAlgorithm KeyAlgorithm { get; set; }
    public int KeySize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Computed status based on revocation and expiration.
    /// </summary>
    public CertificateStatus Status
    {
        get
        {
            if (RevokedAt.HasValue)
                return CertificateStatus.Revoked;
            if (ExpiresAt < DateTime.UtcNow)
                return CertificateStatus.Expired;
            return CertificateStatus.Valid;
        }
    }
}
