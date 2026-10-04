using MiniPKI.Core.Domain;

namespace MiniPKI.Core.Services;

/// <summary>
/// Manages the Root CA and Intermediate CA lifecycle.
/// </summary>
public interface ICertificateAuthorityService
{
    /// <summary>
    /// Ensures the root and intermediate CA certificates and keys exist on disk.
    /// Generates them if missing.
    /// </summary>
    Task EnsureCaInitializedAsync();

    /// <summary>
    /// Returns the PEM-encoded root CA certificate.
    /// </summary>
    Task<string> GetRootCaPemAsync();

    /// <summary>
    /// Returns the PEM-encoded intermediate CA certificate.
    /// </summary>
    Task<string> GetIntermediateCaPemAsync();

    /// <summary>
    /// Returns the PEM-encoded intermediate CA private key.
    /// Used by CertificateService and CrlService for signing.
    /// </summary>
    Task<string> GetIntermediateCaKeyPemAsync();

    /// <summary>
    /// Returns the full CA chain (root + intermediate) in PEM format.
    /// </summary>
    Task<string> GetCaChainPemAsync();
}
