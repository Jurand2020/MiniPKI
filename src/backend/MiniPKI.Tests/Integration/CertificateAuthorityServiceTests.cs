using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Integration;

public class CertificateAuthorityServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _config;
    private readonly FileConfigurationStore _configStore;

    public CertificateAuthorityServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-ca-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _config = new PkiConfiguration { RsaKeySize = 2048 };
        _configStore = new FileConfigurationStore(_paths, _config);
    }

    [Fact]
    public async Task EnsureCaInitializedAsync_GeneratesRootAndIntermediateCerts()
    {
        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);

        await caService.EnsureCaInitializedAsync();

        Assert.True(File.Exists(_paths.RootCertPath));
        Assert.True(File.Exists(_paths.RootKeyPath));
        Assert.True(File.Exists(_paths.IntermediateCertPath));
        Assert.True(File.Exists(_paths.IntermediateKeyPath));
    }

    [Fact]
    public async Task EnsureCaInitializedAsync_LoadsExistingCertsOnSecondCall()
    {
        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);

        await caService.EnsureCaInitializedAsync();
        var rootPem1 = await caService.GetRootCaPemAsync();

        // Create a new service instance pointing to the same directory
        var caService2 = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        await caService2.EnsureCaInitializedAsync();
        var rootPem2 = await caService2.GetRootCaPemAsync();

        Assert.Equal(rootPem1, rootPem2);
    }

    [Fact]
    public async Task GetRootCaPemAsync_ReturnsValidPem()
    {
        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        await caService.EnsureCaInitializedAsync();

        var pem = await caService.GetRootCaPemAsync();

        Assert.Contains("-----BEGIN CERTIFICATE-----", pem);
        Assert.Contains("-----END CERTIFICATE-----", pem);
    }

    [Fact]
    public async Task GetIntermediateCaPemAsync_ReturnsValidPem()
    {
        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        await caService.EnsureCaInitializedAsync();

        var pem = await caService.GetIntermediateCaPemAsync();

        Assert.Contains("-----BEGIN CERTIFICATE-----", pem);
        Assert.Contains("-----END CERTIFICATE-----", pem);
    }

    [Fact]
    public async Task GetCaChainPemAsync_ReturnsBothCerts()
    {
        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        await caService.EnsureCaInitializedAsync();

        var chain = await caService.GetCaChainPemAsync();

        var certCount = chain.Split("-----BEGIN CERTIFICATE-----").Length - 1;
        Assert.Equal(2, certCount);
    }

    [Fact]
    public async Task ResetCa_GeneratesNewCertsWithConfiguredSubjectFields()
    {
        // Save a runtime config with specific subject fields
        var runtimeConfig = new PkiConfiguration
        {
            RsaKeySize = 2048,
            Organization = "MyOrg",
            OrganizationalUnit = "MyOU",
            Country = "US",
            State = "California",
            Locality = "San Francisco"
        };
        await _configStore.SaveAsync(runtimeConfig);

        var caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        await caService.EnsureCaInitializedAsync();

        // Get the root cert before reset
        var rootPemBefore = await caService.GetRootCaPemAsync();

        // Reset CA
        await caService.ResetCaAsync();

        // Get the root cert after reset
        var rootPemAfter = await caService.GetRootCaPemAsync();

        // The certs should be different (new key pair generated)
        Assert.NotEqual(rootPemBefore, rootPemAfter);

        // Verify the root cert subject contains the configured fields
        var rootCert = PemHelper.ReadCertificatePem(rootPemAfter);
        var subjectDn = rootCert.SubjectDN.ToString();
        Assert.Contains("O=MyOrg", subjectDn);
        Assert.Contains("OU=MyOU", subjectDn);
        Assert.Contains("C=US", subjectDn);
        Assert.Contains("ST=California", subjectDn);
        Assert.Contains("L=San Francisco", subjectDn);

        // Verify the intermediate cert subject contains the configured fields
        var intPem = await caService.GetIntermediateCaPemAsync();
        var intCert = PemHelper.ReadCertificatePem(intPem);
        var intSubjectDn = intCert.SubjectDN.ToString();
        Assert.Contains("O=MyOrg", intSubjectDn);
        Assert.Contains("OU=MyOU", intSubjectDn);
        Assert.Contains("C=US", intSubjectDn);
        Assert.Contains("ST=California", intSubjectDn);
        Assert.Contains("L=San Francisco", intSubjectDn);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
