using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;

namespace MiniPKI.Infrastructure.Storage;

/// <summary>
/// JSON-serializable configuration record persisted in the configuration directory.
/// </summary>
public class StoredConfiguration
{
    [JsonPropertyName("defaultDomain")]
    public string DefaultDomain { get; set; } = string.Empty;

    [JsonPropertyName("crlUrl")]
    public string CrlUrl { get; set; } = string.Empty;

    [JsonPropertyName("defaultValidityDays")]
    public int DefaultValidityDays { get; set; }

    [JsonPropertyName("algorithm")]
    public KeyAlgorithm Algorithm { get; set; }

    [JsonPropertyName("rsaKeySize")]
    public int RsaKeySize { get; set; }

    [JsonPropertyName("organization")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("organizationalUnit")]
    public string OrganizationalUnit { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("locality")]
    public string Locality { get; set; } = string.Empty;
}

/// <summary>
/// File-based configuration store. Loads from JSON file, falls back to defaults.
/// </summary>
public class FileConfigurationStore
{
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _defaults;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileConfigurationStore(DataPathProvider paths, PkiConfiguration defaults)
    {
        _paths = paths;
        _defaults = defaults;
    }

    /// <summary>
    /// Loads configuration from file, or returns defaults if file doesn't exist.
    /// </summary>
    public async Task<PkiConfiguration> LoadAsync()
    {
        if (!File.Exists(_paths.ConfigurationFilePath))
        {
            return CloneDefaults();
        }

        var json = await File.ReadAllTextAsync(_paths.ConfigurationFilePath);
        var stored = JsonSerializer.Deserialize(json, StorageJsonContext.Default.StoredConfiguration);
        if (stored == null) return CloneDefaults();

        return new PkiConfiguration
        {
            DefaultDomain = stored.DefaultDomain,
            CrlUrl = stored.CrlUrl,
            DefaultValidityDays = stored.DefaultValidityDays,
            Algorithm = stored.Algorithm,
            RsaKeySize = stored.RsaKeySize,
            Organization = stored.Organization,
            OrganizationalUnit = stored.OrganizationalUnit,
            Country = stored.Country,
            State = stored.State,
            Locality = stored.Locality
        };
    }

    /// <summary>
    /// Saves configuration to file.
    /// </summary>
    public async Task SaveAsync(PkiConfiguration config)
    {
        await _lock.WaitAsync();
        try
        {
            var stored = new StoredConfiguration
            {
                DefaultDomain = config.DefaultDomain,
                CrlUrl = config.CrlUrl,
                DefaultValidityDays = config.DefaultValidityDays,
                Algorithm = config.Algorithm,
                RsaKeySize = config.RsaKeySize,
                Organization = config.Organization,
                OrganizationalUnit = config.OrganizationalUnit,
                Country = config.Country,
                State = config.State,
                Locality = config.Locality
            };
            var json = JsonSerializer.Serialize(stored, StorageJsonContext.Default.StoredConfiguration);
            await File.WriteAllTextAsync(_paths.ConfigurationFilePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    private PkiConfiguration CloneDefaults()
    {
        return new PkiConfiguration
        {
            DefaultDomain = _defaults.DefaultDomain,
            CrlUrl = _defaults.CrlUrl,
            DefaultValidityDays = _defaults.DefaultValidityDays,
            Algorithm = _defaults.Algorithm,
            RsaKeySize = _defaults.RsaKeySize,
            Organization = _defaults.Organization,
            OrganizationalUnit = _defaults.OrganizationalUnit,
            Country = _defaults.Country,
            State = _defaults.State,
            Locality = _defaults.Locality
        };
    }
}
