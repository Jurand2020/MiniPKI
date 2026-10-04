namespace MiniPKI.Core.Domain;

/// <summary>
/// Lifecycle status of an issued certificate.
/// </summary>
public enum CertificateStatus
{
    Valid,
    Revoked,
    Expired
}
