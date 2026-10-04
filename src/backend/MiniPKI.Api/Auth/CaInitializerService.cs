using MiniPKI.Infrastructure.Services;

namespace MiniPKI.Api.Auth;

/// <summary>
/// Initializes the Certificate Authority on application startup.
/// </summary>
public class CaInitializerService : IHostedService
{
    private readonly BouncyCastleCertificateAuthorityService _caService;

    public CaInitializerService(BouncyCastleCertificateAuthorityService caService)
    {
        _caService = caService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _caService.EnsureCaInitializedAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
