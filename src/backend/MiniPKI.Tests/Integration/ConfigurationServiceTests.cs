using MiniPKI.Core.Configuration;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Tests.Integration;

public class ConfigurationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly FileConfigurationStore _store;
    private readonly ConfigurationService _configService;

    public ConfigurationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-config-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _store = new FileConfigurationStore(_paths, new PkiConfiguration());
        _configService = new ConfigurationService(_store);
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaultsWhenNoFile()
    {
        var config = await _configService.GetAsync();

        Assert.Equal("company.local", config.DefaultDomain);
        Assert.Equal(825, config.DefaultValidityDays);
    }

    [Fact]
    public async Task UpdateAsync_SavesConfigurationToFile()
    {
        var newConfig = new PkiConfiguration
        {
            DefaultDomain = "test.local",
            CrlUrl = "http://crl.test.com",
            DefaultValidityDays = 365,
            RsaKeySize = 4096
        };

        await _configService.UpdateAsync(newConfig);

        Assert.True(File.Exists(_paths.ConfigurationFilePath));
    }

    [Fact]
    public async Task UpdateAsync_ThenGetAsync_ReturnsUpdatedValues()
    {
        var newConfig = new PkiConfiguration
        {
            DefaultDomain = "updated.local",
            CrlUrl = "http://crl.updated.com",
            DefaultValidityDays = 730,
            RsaKeySize = 8192
        };

        await _configService.UpdateAsync(newConfig);
        var retrieved = await _configService.GetAsync();

        Assert.Equal("updated.local", retrieved.DefaultDomain);
        Assert.Equal(730, retrieved.DefaultValidityDays);
        Assert.Equal(8192, retrieved.RsaKeySize);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
