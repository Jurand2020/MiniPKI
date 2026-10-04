using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Integration;

public class CertificateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _config;
    private readonly FileConfigurationStore _configStore;
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _store;
    private readonly FileAuditService _audit;
    private readonly BouncyCastleCertificateService _certService;

    public CertificateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-cert-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _config = new PkiConfiguration { RsaKeySize = 2048, CrlUrl = "http://crl.test.com" };
        _configStore = new FileConfigurationStore(_paths, _config);
        _caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        _store = new FileCertificateStore(_paths);
        _audit = new FileAuditService(_paths);
        var crlService = new BouncyCastleCrlService(_caService, _store, _audit, _paths);
        _certService = new BouncyCastleCertificateService(_caService, _store, _audit, _config, _configStore, crlService);

        _caService.EnsureCaInitializedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task IssueAsync_CreatesCertificateFiles()
    {
        var record = await _certService.IssueAsync(
            "test.example.com",
            new List<string> { "test.example.com" },
            KeyAlgorithm.RSA, 2048, 365);

        Assert.True(File.Exists(_paths.GetCertificatePath(record.SerialNumber)));
        Assert.True(File.Exists(_paths.GetKeyPath(record.SerialNumber)));
        Assert.True(File.Exists(_paths.GetMetadataPath(record.SerialNumber)));
    }

    [Fact]
    public async Task IssueAsync_ReturnsValidRecord()
    {
        var record = await _certService.IssueAsync(
            "test.example.com",
            new List<string> { "test.example.com" },
            KeyAlgorithm.RSA, 2048, 365);

        Assert.False(string.IsNullOrEmpty(record.SerialNumber));
        Assert.Equal("test.example.com", record.CommonName);
        Assert.Equal(KeyAlgorithm.RSA, record.KeyAlgorithm);
        Assert.Equal(2048, record.KeySize);
        Assert.Equal(CertificateStatus.Valid, record.Status);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllIssuedCertificates()
    {
        await _certService.IssueAsync("a.test.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);
        await _certService.IssueAsync("b.test.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var list = await _certService.ListAsync();

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetBySerialAsync_ReturnsCorrectRecord()
    {
        var issued = await _certService.IssueAsync(
            "test.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var retrieved = await _certService.GetBySerialAsync(issued.SerialNumber);

        Assert.NotNull(retrieved);
        Assert.Equal(issued.SerialNumber, retrieved.SerialNumber);
        Assert.Equal(issued.CommonName, retrieved.CommonName);
    }

    [Fact]
    public async Task GetBySerialAsync_ReturnsNullForNonExistent()
    {
        var result = await _certService.GetBySerialAsync("NONEXISTENT");

        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_MovesFilesToRevokedDirectory()
    {
        var record = await _certService.IssueAsync(
            "test.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var result = await _certService.RevokeAsync(record.SerialNumber, "keyCompromise");

        Assert.True(result);
        Assert.False(File.Exists(_paths.GetCertificatePath(record.SerialNumber)));
        Assert.True(File.Exists(_paths.GetRevokedCertificatePath(record.SerialNumber)));
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalseForNonExistent()
    {
        var result = await _certService.RevokeAsync("NONEXISTENT", "test");

        Assert.False(result);
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalseForAlreadyRevoked()
    {
        var record = await _certService.IssueAsync(
            "test.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        await _certService.RevokeAsync(record.SerialNumber, "keyCompromise");
        var result = await _certService.RevokeAsync(record.SerialNumber, "keyCompromise");

        Assert.False(result);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsCertificateKeyAndChain()
    {
        var record = await _certService.IssueAsync(
            "test.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var (certPem, keyPem, chainPem) = await _certService.DownloadAsync(record.SerialNumber);

        Assert.Contains("-----BEGIN CERTIFICATE-----", certPem);
        Assert.Contains("-----BEGIN PRIVATE KEY-----", keyPem);
        Assert.Contains("-----BEGIN CERTIFICATE-----", chainPem);
    }

    [Fact]
    public async Task IssueAsync_WithEcdsa_CreatesEcdsaCertificate()
    {
        var record = await _certService.IssueAsync(
            "ecdsa.test.com", new List<string>(), KeyAlgorithm.ECDSA, 256, 365);

        Assert.Equal(KeyAlgorithm.ECDSA, record.KeyAlgorithm);
        Assert.True(File.Exists(_paths.GetCertificatePath(record.SerialNumber)));
    }

    [Fact]
    public async Task IssueAsync_CrlUrlMatchesConfiguredUrl()
    {
        // Save a runtime config with a specific CRL URL
        var runtimeConfig = new PkiConfiguration
        {
            RsaKeySize = 2048,
            CrlUrl = "http://custom.crl.url"
        };
        await _configStore.SaveAsync(runtimeConfig);

        var record = await _certService.IssueAsync(
            "crltest.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var certPem = await _store.GetCertificatePemAsync(record.SerialNumber);
        var cert = PemHelper.ReadCertificatePem(certPem);

        // The CRL distribution point URL should match the configured CrlUrl
        var crlDpExt = cert.GetExtensionValue(Org.BouncyCastle.Asn1.X509.X509Extensions.CrlDistributionPoints);
        Assert.NotNull(crlDpExt);
        var crlDpData = crlDpExt.GetOctets();
        var crlDp = Org.BouncyCastle.Asn1.X509.CrlDistPoint.GetInstance(
            Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(crlDpData));
        var dp = crlDp.GetDistributionPoints()[0];
        var name = dp.DistributionPointName;
        var gens = Org.BouncyCastle.Asn1.X509.GeneralNames.GetInstance(name.Name);
        var url = gens.GetNames()[0].Name.ToString();

        Assert.Equal("http://custom.crl.url", url);
    }

    [Fact]
    public async Task IssueAsync_CrlUrlResolvesApiCrlPlaceholder()
    {
        // Save a runtime config with {API_CRL} placeholder
        var runtimeConfig = new PkiConfiguration
        {
            RsaKeySize = 2048,
            CrlUrl = "http://my.host.local:11111{API_CRL}"
        };
        await _configStore.SaveAsync(runtimeConfig);

        var record = await _certService.IssueAsync(
            "placeholder.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        var certPem = await _store.GetCertificatePemAsync(record.SerialNumber);
        var cert = PemHelper.ReadCertificatePem(certPem);

        var crlDpExt = cert.GetExtensionValue(Org.BouncyCastle.Asn1.X509.X509Extensions.CrlDistributionPoints);
        Assert.NotNull(crlDpExt);
        var crlDpData = crlDpExt.GetOctets();
        var crlDp = Org.BouncyCastle.Asn1.X509.CrlDistPoint.GetInstance(
            Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(crlDpData));
        var dp = crlDp.GetDistributionPoints()[0];
        var name = dp.DistributionPointName;
        var gens = Org.BouncyCastle.Asn1.X509.GeneralNames.GetInstance(name.Name);
        var url = gens.GetNames()[0].Name.ToString();

        Assert.Equal("http://my.host.local:11111/api/crl/current", url);
    }

    [Fact]
    public async Task RevokeAsync_RegeneratesCrlWithRevokedCertificate()
    {
        // Issue a certificate
        var record = await _certService.IssueAsync(
            "crl-update.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        // Generate an initial CRL (empty - no revoked certs yet)
        var crlService = new BouncyCastleCrlService(_caService, _store, _audit, _paths);
        var initialCrlPem = await crlService.GenerateCrlAsync();
        var initialCrl = PemHelper.ReadCrlPem(initialCrlPem);
        var initialRevoked = initialCrl.GetRevokedCertificates();
        Assert.True(initialRevoked == null || initialRevoked.Count == 0,
            "Initial CRL should have no revoked certificates");

        // Revoke the certificate - this should regenerate the CRL
        await _certService.RevokeAsync(record.SerialNumber, "keyCompromise");

        // Read the CRL file that was regenerated by RevokeAsync
        var updatedCrlPem = await File.ReadAllTextAsync(_paths.CrlFilePath);
        var updatedCrl = PemHelper.ReadCrlPem(updatedCrlPem);
        var updatedRevoked = updatedCrl.GetRevokedCertificates();
        Assert.NotNull(updatedRevoked);
        Assert.NotEmpty(updatedRevoked);

        // Verify the revoked serial is in the CRL
        var expectedSerial = new Org.BouncyCastle.Math.BigInteger(record.SerialNumber, 16);
        var found = false;
        foreach (var entry in updatedRevoked)
        {
            if (entry.SerialNumber.Equals(expectedSerial))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, $"CRL should contain revoked serial {record.SerialNumber}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
