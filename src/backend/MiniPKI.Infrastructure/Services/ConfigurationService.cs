using MiniPKI.Core.Configuration;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Infrastructure.Services;

/// <summary>
/// Configuration service that reads and updates the runtime PKI configuration.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly FileConfigurationStore _store;

    public ConfigurationService(FileConfigurationStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Gets the current PKI configuration.
    /// </summary>
    public async Task<PkiConfiguration> GetAsync()
    {
        return await _store.LoadAsync();
    }

    /// <summary>
    /// Updates the PKI configuration.
    /// </summary>
    public async Task<PkiConfiguration> UpdateAsync(PkiConfiguration configuration)
    {
        await _store.SaveAsync(configuration);
        return configuration;
    }
}
