using Microsoft.AspNetCore.Mvc;
using MiniPKI.Api.Models;
using MiniPKI.Core.Domain;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/certificates")]
public class CertificatesController : ControllerBase
{
    private readonly BouncyCastleCertificateService _certService;
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly IAuditService _audit;

    public CertificatesController(
        BouncyCastleCertificateService certService,
        BouncyCastleCertificateAuthorityService caService,
        IAuditService audit)
    {
        _certService = certService;
        _caService = caService;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<CertificateResponse>>> List()
    {
        var records = await _certService.ListAsync();
        return Ok(records.Select(ToResponse));
    }

    [HttpGet("{serial}")]
    public async Task<ActionResult<CertificateResponse>> Get(string serial)
    {
        var record = await _certService.GetBySerialAsync(serial);
        if (record == null) return NotFound();
        return Ok(ToResponse(record));
    }

    [HttpPost]
    public async Task<ActionResult<CertificateResponse>> Issue([FromBody] IssueCertificateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommonName))
        {
            return BadRequest(new { error = "CommonName is required" });
        }

        var algorithm = request.KeyAlgorithm?.ToUpperInvariant() == "ECDSA"
            ? KeyAlgorithm.ECDSA
            : KeyAlgorithm.RSA;

        var keySize = request.KeySize ?? 4096;
        var validityDays = request.ValidityDays ?? 825;

        var record = await _certService.IssueAsync(
            request.CommonName,
            request.SanEntries,
            algorithm,
            keySize,
            validityDays);

        return CreatedAtAction(nameof(Get), new { serial = record.SerialNumber }, ToResponse(record));
    }

    [HttpPost("csr")]
    public async Task<ActionResult<CertificateResponse>> IssueFromCsr([FromBody] IssueCsrRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CsrPem))
        {
            return BadRequest(new { error = "CSR PEM is required" });
        }

        var validityDays = request.ValidityDays ?? 825;

        var record = await _certService.IssueFromCsrAsync(request.CsrPem, validityDays);

        return CreatedAtAction(nameof(Get), new { serial = record.SerialNumber }, ToResponse(record));
    }

    [HttpPost("{serial}/revoke")]
    public async Task<ActionResult> Revoke(string serial, [FromBody] RevokeCertificateRequest request)
    {
        var result = await _certService.RevokeAsync(serial, request.Reason);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpGet("{serial}/download")]
    public async Task<ActionResult> Download(string serial)
    {
        var (certPem, _, _) = await _certService.DownloadAsync(serial);
        return Content(certPem, "application/x-pem-file");
    }

    [HttpGet("{serial}/bundle")]
    public async Task<ActionResult> Bundle(string serial)
    {
        var (certPem, _, chainPem) = await _certService.DownloadAsync(serial);
        var bundle = certPem + chainPem;
        return Content(bundle, "application/x-pem-file");
    }

    [HttpGet("{serial}/chain")]
    public async Task<ActionResult> Chain(string serial)
    {
        var (_, _, chainPem) = await _certService.DownloadAsync(serial);
        return Content(chainPem, "application/x-pem-file");
    }

    [HttpGet("{serial}/key")]
    public async Task<ActionResult> Key(string serial)
    {
        var (_, keyPem, _) = await _certService.DownloadAsync(serial);
        if (string.IsNullOrEmpty(keyPem))
        {
            return NotFound(new { error = "Private key not available (CSR-issued certificate)" });
        }
        return Content(keyPem, "application/x-pem-file");
    }

    [HttpGet("{serial}/p12")]
    public async Task<ActionResult> DownloadP12(string serial, [FromQuery] string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return BadRequest(new { error = "Password is required for P12 download" });
        }

        var (certPem, keyPem, chainPem) = await _certService.DownloadAsync(serial);
        if (string.IsNullOrEmpty(keyPem))
        {
            return NotFound(new { error = "Private key not available (CSR-issued certificate)" });
        }

        var cert = PemHelper.ReadCertificatePem(certPem);
        var privateKey = PemHelper.ReadPrivateKeyPem(keyPem);

        // Parse chain certificates
        var chainCerts = ParseChainCerts(chainPem);

        var p12Bytes = PemHelper.WritePkcs12(cert, privateKey, chainCerts, password);

        return File(p12Bytes, "application/x-pkcs12", $"{serial}.p12");
    }

    private static X509Certificate[] ParseChainCerts(string chainPem)
    {
        var certs = new List<X509Certificate>();
        var parts = chainPem.Split("-----END CERTIFICATE-----", StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var pem = part.Trim() + "\n-----END CERTIFICATE-----";
            if (pem.Contains("-----BEGIN CERTIFICATE-----"))
            {
                try
                {
                    certs.Add(PemHelper.ReadCertificatePem(pem));
                }
                catch { /* skip invalid */ }
            }
        }
        return certs.ToArray();
    }

    private static CertificateResponse ToResponse(CertificateRecord r)
    {
        return new CertificateResponse
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
        };
    }
}
