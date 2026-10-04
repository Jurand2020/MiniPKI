namespace MiniPKI.Infrastructure.Storage;

/// <summary>
/// Centralizes all file path logic for the PKI data directory.
/// </summary>
public class DataPathProvider
{
    public string BasePath { get; }

    public string CaPath => Path.Combine(BasePath, "ca");
    public string CertificatesPath => Path.Combine(BasePath, "certificates");
    public string RevokedPath => Path.Combine(BasePath, "revoked");
    public string CrlPath => Path.Combine(BasePath, "crl");
    public string AuditPath => Path.Combine(BasePath, "audit");
    public string ConfigurationPath => Path.Combine(BasePath, "configuration");

    public string RootCertPath => Path.Combine(CaPath, "root.crt");
    public string RootKeyPath => Path.Combine(CaPath, "root.key");
    public string IntermediateCertPath => Path.Combine(CaPath, "intermediate.crt");
    public string IntermediateKeyPath => Path.Combine(CaPath, "intermediate.key");

    public string CrlFilePath => Path.Combine(CrlPath, "ca.crl");
    public string ConfigurationFilePath => Path.Combine(ConfigurationPath, "pki.json");
    public string PasswordFilePath => Path.Combine(ConfigurationPath, "password.hash");

    public string GetCertificatePath(string serial) => Path.Combine(CertificatesPath, $"{serial}.crt");
    public string GetKeyPath(string serial) => Path.Combine(CertificatesPath, $"{serial}.key");
    public string GetMetadataPath(string serial) => Path.Combine(CertificatesPath, $"{serial}.json");

    public string GetRevokedCertificatePath(string serial) => Path.Combine(RevokedPath, $"{serial}.crt");
    public string GetRevokedKeyPath(string serial) => Path.Combine(RevokedPath, $"{serial}.key");
    public string GetRevokedMetadataPath(string serial) => Path.Combine(RevokedPath, $"{serial}.json");

    public DataPathProvider(string basePath)
    {
        BasePath = basePath;
    }

    /// <summary>
    /// Creates all required directories under the data path.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        foreach (var dir in new[] { CaPath, CertificatesPath, RevokedPath, CrlPath, AuditPath, ConfigurationPath })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
