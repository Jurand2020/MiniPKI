namespace MiniPKI.Core.Services;

/// <summary>
/// Generates and publishes Certificate Revocation Lists (CRLs).
/// </summary>
public interface ICrlService
{
    /// <summary>
    /// Regenerates the CRL from the current revoked-certificate list.
    /// Returns the PEM-encoded CRL.
    /// </summary>
    Task<string> GenerateCrlAsync();

    /// <summary>
    /// Returns the current CRL in PEM format, generating it if it does not exist.
    /// </summary>
    Task<string> GetCurrentCrlPemAsync();
}
