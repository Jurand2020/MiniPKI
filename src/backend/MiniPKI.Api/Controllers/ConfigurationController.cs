using Microsoft.AspNetCore.Mvc;
using MiniPKI.Core.Configuration;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/configuration")]
public class ConfigurationController : ControllerBase
{
    private readonly FileConfigurationStore _store;
    private readonly IAuditService _audit;
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _certStore;
    private readonly DataPathProvider _paths;

    public ConfigurationController(
        FileConfigurationStore store,
        IAuditService audit,
        BouncyCastleCertificateAuthorityService caService,
        FileCertificateStore certStore,
        DataPathProvider paths)
    {
        _store = store;
        _audit = audit;
        _caService = caService;
        _certStore = certStore;
        _paths = paths;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var config = await _store.LoadAsync();
        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult> Update([FromBody] PkiConfiguration config)
    {
        await _store.SaveAsync(config);
        await _audit.LogAsync("CONFIG_UPDATE", "Configuration updated");
        return Ok(config);
    }

    [HttpPost("reset-ca")]
    public async Task<ActionResult> ResetCa()
    {
        // Delete all issued certificates
        if (Directory.Exists(_paths.CertificatesPath))
        {
            foreach (var file in Directory.GetFiles(_paths.CertificatesPath))
                System.IO.File.Delete(file);
        }

        // Delete all revoked certificates
        if (Directory.Exists(_paths.RevokedPath))
        {
            foreach (var file in Directory.GetFiles(_paths.RevokedPath))
                System.IO.File.Delete(file);
        }

        // Delete CRL
        if (System.IO.File.Exists(_paths.CrlFilePath))
            System.IO.File.Delete(_paths.CrlFilePath);

        // Reset CA (deletes old CA certs/keys, generates new ones)
        await _caService.ResetCaAsync();

        await _audit.LogAsync("CA_RESET",
            "Certificate Authority reset: all issued certificates, revoked certificates, CRL, and CA certs/keys were deleted and new CA was generated");

        return Ok(new { success = true });
    }
}
