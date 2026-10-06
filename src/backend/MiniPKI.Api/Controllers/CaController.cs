using Microsoft.AspNetCore.Mvc;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Services;

namespace MiniPKI.Api.Controllers;

/// <summary>
/// Endpoints for downloading CA certificates (public keys only).
/// </summary>
[ApiController]
[Route("api/ca")]
public class CaController : ControllerBase
{
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly IAuditService _audit;

    public CaController(
        BouncyCastleCertificateAuthorityService caService,
        IAuditService audit)
    {
        _caService = caService;
        _audit = audit;
    }

    /// <summary>
    /// Downloads the full CA chain (intermediate + root) in PEM format.
    /// </summary>
    [HttpGet("chain")]
    public async Task<ActionResult> Chain()
    {
        var chainPem = await _caService.GetCaChainPemAsync();
        return Content(chainPem, "application/x-pem-file");
    }

    /// <summary>
    /// Downloads the Root CA certificate in PEM format.
    /// </summary>
    [HttpGet("root")]
    public async Task<ActionResult> Root()
    {
        var rootPem = await _caService.GetRootCaPemAsync();
        return Content(rootPem, "application/x-pem-file");
    }

    /// <summary>
    /// Downloads the Intermediate CA certificate in PEM format.
    /// </summary>
    [HttpGet("intermediate")]
    public async Task<ActionResult> Intermediate()
    {
        var intPem = await _caService.GetIntermediateCaPemAsync();
        return Content(intPem, "application/x-pem-file");
    }
}
