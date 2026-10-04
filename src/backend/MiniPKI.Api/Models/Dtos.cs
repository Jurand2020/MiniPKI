using System.Text.Json.Serialization;
using MiniPKI.Core.Domain;

namespace MiniPKI.Api.Models;

/// <summary>
/// Login request DTO.
/// </summary>
public class LoginRequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Login response DTO.
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Auth status response DTO.
/// </summary>
public class AuthStatusResponse
{
    public bool Authenticated { get; set; }
    public bool FirstRun { get; set; }
}

/// <summary>
/// Change password request DTO.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Set initial password request DTO (first run).
/// </summary>
public class SetPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Issue certificate request DTO.
/// </summary>
public class IssueCertificateRequest
{
    public string CommonName { get; set; } = string.Empty;
    public List<string> SanEntries { get; set; } = new();
    public string? KeyAlgorithm { get; set; }
    public int? KeySize { get; set; }
    public int? ValidityDays { get; set; }
}

/// <summary>
/// Issue certificate from CSR request DTO.
/// </summary>
public class IssueCsrRequest
{
    public string CsrPem { get; set; } = string.Empty;
    public int? ValidityDays { get; set; }
}

/// <summary>
/// Revoke certificate request DTO.
/// </summary>
public class RevokeCertificateRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Update configuration request DTO.
/// </summary>
public class UpdateConfigurationRequest
{
    public string DefaultDomain { get; set; } = string.Empty;
    public string CrlUrl { get; set; } = string.Empty;
    public int DefaultValidityDays { get; set; }
    public string Algorithm { get; set; } = "RSA";
    public int RsaKeySize { get; set; }
    public string Organization { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
}

/// <summary>
/// Certificate response DTO (for API responses).
/// </summary>
public class CertificateResponse
{
    public string SerialNumber { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public List<string> SanEntries { get; set; } = new();
    public string KeyAlgorithm { get; set; } = string.Empty;
    public int KeySize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    public CertificateStatus Status { get; set; }

    public string Issuer { get; set; } = string.Empty;
}

/// <summary>
/// Audit entry response DTO.
/// </summary>
public class AuditEntryResponse
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? Actor { get; set; }
}

/// <summary>
/// Dashboard statistics response DTO.
/// </summary>
public class DashboardStatsResponse
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Revoked { get; set; }
    public int ExpiringSoon { get; set; }
    public List<CertificateResponse> LatestIssued { get; set; } = new();
}
