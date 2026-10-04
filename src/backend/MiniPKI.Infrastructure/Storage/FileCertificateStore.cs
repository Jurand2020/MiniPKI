using System.Text.Json;
using MiniPKI.Core.Domain;

namespace MiniPKI.Infrastructure.Storage;

/// <summary>
/// File-based store for certificate records (metadata + PEM files).
/// </summary>
public class FileCertificateStore
{
    private readonly DataPathProvider _paths;

    public FileCertificateStore(DataPathProvider paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Saves a certificate record: metadata JSON, certificate PEM, and private key PEM.
    /// </summary>
    public async Task SaveAsync(CertificateRecord record, string certificatePem, string privateKeyPem)
    {
        await File.WriteAllTextAsync(_paths.GetCertificatePath(record.SerialNumber), certificatePem);
        await File.WriteAllTextAsync(_paths.GetKeyPath(record.SerialNumber), privateKeyPem);

        var metadata = new StoredMetadata
        {
            SerialNumber = record.SerialNumber,
            CommonName = record.CommonName,
            SanEntries = record.SanEntries,
            KeyAlgorithm = record.KeyAlgorithm,
            KeySize = record.KeySize,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            Issuer = record.Issuer,
            RevokedAt = record.RevokedAt,
            RevocationReason = record.RevocationReason
        };
        var json = JsonSerializer.Serialize(metadata, StorageJsonContext.Default.StoredMetadata);
        await File.WriteAllTextAsync(_paths.GetMetadataPath(record.SerialNumber), json);
    }

    /// <summary>
    /// Lists all certificate records (valid and revoked).
    /// </summary>
    public async Task<List<CertificateRecord>> ListAsync()
    {
        var records = new List<CertificateRecord>();

        // Active certificates
        if (Directory.Exists(_paths.CertificatesPath))
        {
            foreach (var metaFile in Directory.GetFiles(_paths.CertificatesPath, "*.json"))
            {
                var json = await File.ReadAllTextAsync(metaFile);
                var meta = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredMetadata);
                if (meta == null) continue;
                records.Add(ToRecord(meta));
            }
        }

        // Revoked certificates
        if (Directory.Exists(_paths.RevokedPath))
        {
            foreach (var metaFile in Directory.GetFiles(_paths.RevokedPath, "*.json"))
            {
                var json = await File.ReadAllTextAsync(metaFile);
                var meta = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredMetadata);
                if (meta == null) continue;
                records.Add(ToRecord(meta));
            }
        }

        return records.OrderBy(r => r.CreatedAt).ToList();
    }

    /// <summary>
    /// Retrieves a certificate record by serial number. Returns null if not found.
    /// Checks both active and revoked certificate directories.
    /// </summary>
    public async Task<CertificateRecord?> GetBySerialAsync(string serialNumber)
    {
        // Check active certificates first
        var metaPath = _paths.GetMetadataPath(serialNumber);
        if (File.Exists(metaPath))
        {
            var json = await File.ReadAllTextAsync(metaPath);
            var meta = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredMetadata);
            if (meta != null) return ToRecord(meta);
        }

        // Check revoked certificates
        var revokedMetaPath = _paths.GetRevokedMetadataPath(serialNumber);
        if (File.Exists(revokedMetaPath))
        {
            var json = await File.ReadAllTextAsync(revokedMetaPath);
            var meta = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredMetadata);
            if (meta != null) return ToRecord(meta);
        }

        return null;
    }

    /// <summary>
    /// Reads the PEM-encoded certificate for the given serial.
    /// Checks both active and revoked certificate directories.
    /// </summary>
    public async Task<string> GetCertificatePemAsync(string serialNumber)
    {
        var certPath = _paths.GetCertificatePath(serialNumber);
        if (File.Exists(certPath))
            return await File.ReadAllTextAsync(certPath);

        var revokedCertPath = _paths.GetRevokedCertificatePath(serialNumber);
        if (File.Exists(revokedCertPath))
            return await File.ReadAllTextAsync(revokedCertPath);

        throw new FileNotFoundException($"Certificate not found: {serialNumber}");
    }

    /// <summary>
    /// Reads the PEM-encoded private key for the given serial.
    /// Checks both active and revoked certificate directories.
    /// </summary>
    public async Task<string> GetPrivateKeyPemAsync(string serialNumber)
    {
        var keyPath = _paths.GetKeyPath(serialNumber);
        if (File.Exists(keyPath))
            return await File.ReadAllTextAsync(keyPath);

        var revokedKeyPath = _paths.GetRevokedKeyPath(serialNumber);
        if (File.Exists(revokedKeyPath))
            return await File.ReadAllTextAsync(revokedKeyPath);

        throw new FileNotFoundException($"Private key not found: {serialNumber}");
    }

    /// <summary>
    /// Marks a certificate as revoked: moves files to revoked/ and updates metadata.
    /// </summary>
    public async Task<bool> RevokeAsync(string serialNumber, string reason)
    {
        var record = await GetBySerialAsync(serialNumber);
        if (record == null || record.Status == CertificateStatus.Revoked) return false;

        record.RevokedAt = DateTime.UtcNow;
        record.RevocationReason = reason;

        // Move files to revoked directory
        var certPath = _paths.GetCertificatePath(serialNumber);
        var keyPath = _paths.GetKeyPath(serialNumber);
        var metaPath = _paths.GetMetadataPath(serialNumber);

        var revokedCertPath = _paths.GetRevokedCertificatePath(serialNumber);
        var revokedKeyPath = _paths.GetRevokedKeyPath(serialNumber);
        var revokedMetaPath = _paths.GetRevokedMetadataPath(serialNumber);

        if (File.Exists(certPath)) File.Move(certPath, revokedCertPath, overwrite: true);
        if (File.Exists(keyPath)) File.Move(keyPath, revokedKeyPath, overwrite: true);
        if (File.Exists(metaPath)) File.Move(metaPath, revokedMetaPath, overwrite: true);

        // Update metadata with revocation info
        var metadata = new StoredMetadata
        {
            SerialNumber = record.SerialNumber,
            CommonName = record.CommonName,
            SanEntries = record.SanEntries,
            KeyAlgorithm = record.KeyAlgorithm,
            KeySize = record.KeySize,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            Issuer = record.Issuer,
            RevokedAt = record.RevokedAt,
            RevocationReason = record.RevocationReason
        };
        var json = JsonSerializer.Serialize(metadata, StorageJsonContext.Default.StoredMetadata);
        await File.WriteAllTextAsync(revokedMetaPath, json);

        return true;
    }

    /// <summary>
    /// Lists all revoked certificate records.
    /// </summary>
    public async Task<List<CertificateRecord>> ListRevokedAsync()
    {
        var records = new List<CertificateRecord>();
        if (!Directory.Exists(_paths.RevokedPath)) return records;

        foreach (var metaFile in Directory.GetFiles(_paths.RevokedPath, "*.json"))
        {
            var json = await File.ReadAllTextAsync(metaFile);
            var meta = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredMetadata);
            if (meta == null) continue;
            records.Add(ToRecord(meta));
        }

        return records.OrderBy(r => r.RevokedAt).ToList();
    }

    private static CertificateRecord ToRecord(StoredMetadata meta)
    {
        return new CertificateRecord
        {
            SerialNumber = meta.SerialNumber,
            CommonName = meta.CommonName,
            SanEntries = meta.SanEntries,
            KeyAlgorithm = meta.KeyAlgorithm,
            KeySize = meta.KeySize,
            CreatedAt = meta.CreatedAt,
            ExpiresAt = meta.ExpiresAt,
            Issuer = meta.Issuer,
            RevokedAt = meta.RevokedAt,
            RevocationReason = meta.RevocationReason
        };
    }
}
