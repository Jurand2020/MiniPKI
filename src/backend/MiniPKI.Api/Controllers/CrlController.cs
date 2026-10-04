using Microsoft.AspNetCore.Mvc;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Services;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/crl")]
public class CrlController : ControllerBase
{
    private readonly BouncyCastleCrlService _crlService;
    private readonly IAuditService _audit;

    public CrlController(BouncyCastleCrlService crlService, IAuditService audit)
    {
        _crlService = crlService;
        _audit = audit;
    }

    [HttpPost("generate")]
    public async Task<ActionResult> Generate()
    {
        var crlPem = await _crlService.GenerateCrlAsync();
        await _audit.LogAsync("CRL_GENERATE", "CRL regenerated via API");
        return Content(crlPem, "application/x-pem-file");
    }

    [HttpGet("current")]
    public async Task<ActionResult> Current()
    {
        var crlPem = await _crlService.GetCurrentCrlPemAsync();
        return Content(crlPem, "application/x-pem-file");
    }
}
