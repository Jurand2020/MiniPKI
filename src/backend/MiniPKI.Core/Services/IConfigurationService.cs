using MiniPKI.Core.Configuration;

namespace MiniPKI.Core.Services;

/// <summary>
/// Reads and updates the runtime PKI configuration.
/// </summary>
public interface IConfigurationService
{
    Task<PkiConfiguration> GetAsync();
    Task<PkiConfiguration> UpdateAsync(PkiConfiguration configuration);
}
