using MiniPKI.Core.Domain;

namespace MiniPKI.Core.Services;

/// <summary>
/// Issues, revokes, lists, and retrieves end-entity TLS certificates.
/// </summary>
public interface ICertificateService
{
    Task<List<CertificateRecord>> ListAsync();
    Task<CertificateRecord?> GetBySerialAsync(string serialNumber);
    Task<CertificateRecord> IssueAsync(string commonName, List<string> sanEntries, KeyAlgorithm algorithm, int keySize, int validityDays);
    Task<bool> RevokeAsync(string serialNumber, string reason);
    Task<(string certificatePem, string keyPem, string chainPem)> DownloadAsync(string serialNumber);
}
