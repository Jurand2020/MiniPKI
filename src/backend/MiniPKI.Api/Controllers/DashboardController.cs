using Microsoft.AspNetCore.Mvc;
using MiniPKI.Api.Models;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Services;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly BouncyCastleCertificateService _certService;

    public DashboardController(BouncyCastleCertificateService certService)
    {
        _certService = certService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> Stats()
    {
        var records = await _certService.ListAsync();
        var now = DateTime.UtcNow;
        var expiringThreshold = now.AddDays(30);

        var active = records.Count(r => r.Status == CertificateStatus.Valid);
        var revoked = records.Count(r => r.Status == CertificateStatus.Revoked);
        var expiringSoon = records.Count(r =>
            r.Status == CertificateStatus.Valid && r.ExpiresAt <= expiringThreshold);

        return Ok(new DashboardStatsResponse
        {
            Total = records.Count,
            Active = active,
            Revoked = revoked,
            ExpiringSoon = expiringSoon,
            LatestIssued = records
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new CertificateResponse
                {
                    SerialNumber = r.SerialNumber,
                    CommonName = r.CommonName,
                    SanEntries = r.SanEntries,
                    KeyAlgorithm = r.KeyAlgorithm.ToString(),
                    KeySize = r.KeySize,
                    CreatedAt = r.CreatedAt,
                    ExpiresAt = r.ExpiresAt,
                    RevokedAt = r.RevokedAt,
                    RevocationReason = r.RevocationReason,
                    Status = r.Status,
                    Issuer = r.Issuer
                })
                .ToList()
        });
    }
}
