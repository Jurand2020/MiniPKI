using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Integration;

public class CrlServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _config;
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _store;
    private readonly FileAuditService _audit;
    private readonly BouncyCastleCrlService _crlService;

    public CrlServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-crl-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _config = new PkiConfiguration { RsaKeySize = 2048, CrlUrl = "http://crl.test.com" };
        _caService = new BouncyCastleCertificateAuthorityService(_paths, _config, new FileConfigurationStore(_paths, _config));
        _store = new FileCertificateStore(_paths);
        _audit = new FileAuditService(_paths);
        _crlService = new BouncyCastleCrlService(_caService, _store, _audit, _paths);

        _caService.EnsureCaInitializedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GenerateCrlAsync_CreatesCrlFile()
    {
        await _crlService.GenerateCrlAsync();

        Assert.True(File.Exists(_paths.CrlFilePath));
    }

    [Fact]
    public async Task GenerateCrlAsync_ReturnsValidPem()
    {
        var crlPem = await _crlService.GenerateCrlAsync();

        Assert.Contains("-----BEGIN X509 CRL-----", crlPem);
        Assert.Contains("-----END X509 CRL-----", crlPem);
    }

    [Fact]
    public async Task GenerateCrlAsync_WithNoRevokedCertificates()
    {
        var crlPem = await _crlService.GenerateCrlAsync();

        Assert.NotNull(crlPem);
        Assert.NotEmpty(crlPem);
    }

    [Fact]
    public async Task GetCurrentCrlPemAsync_ReturnsExistingCrl()
    {
        var generatedPem = await _crlService.GenerateCrlAsync();
        var currentPem = await _crlService.GetCurrentCrlPemAsync();

        Assert.Equal(generatedPem, currentPem);
    }

    [Fact]
    public async Task GetCurrentCrlPemAsync_GeneratesIfNotExists()
    {
        Assert.False(File.Exists(_paths.CrlFilePath));

        var crlPem = await _crlService.GetCurrentCrlPemAsync();

        Assert.NotNull(crlPem);
        Assert.True(File.Exists(_paths.CrlFilePath));
    }

    [Fact]
    public async Task GenerateCrlAsync_ContainsRevokedCertificates()
    {
        // Issue a certificate
        var certService = new BouncyCastleCertificateService(
            _caService, _store, _audit, _config, new FileConfigurationStore(_paths, _config), _crlService);
        var record = await certService.IssueAsync(
            "revoked.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        // Revoke it
        await _store.RevokeAsync(record.SerialNumber, "keyCompromise");

        // Generate CRL
        var crlPem = await _crlService.GenerateCrlAsync();

        // Parse the CRL and verify it contains the revoked serial
        var crl = PemHelper.ReadCrlPem(crlPem);
        var revokedEntries = crl.GetRevokedCertificates();
        Assert.NotNull(revokedEntries);
        Assert.NotEmpty(revokedEntries);

        var expectedSerial = new Org.BouncyCastle.Math.BigInteger(record.SerialNumber, 16);
        var found = false;
        foreach (var entry in revokedEntries)
        {
            if (entry.SerialNumber.Equals(expectedSerial))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, $"CRL should contain revoked serial {record.SerialNumber}");
    }

    [Fact]
    public async Task GenerateCrlAsync_DoesNotContainNonRevokedCertificates()
    {
        // Issue a certificate but do NOT revoke it
        var certService = new BouncyCastleCertificateService(
            _caService, _store, _audit, _config, new FileConfigurationStore(_paths, _config), _crlService);
        var record = await certService.IssueAsync(
            "valid.example.com", new List<string>(), KeyAlgorithm.RSA, 2048, 365);

        // Generate CRL
        var crlPem = await _crlService.GenerateCrlAsync();

        // Parse the CRL and verify it does NOT contain the valid serial
        var crl = PemHelper.ReadCrlPem(crlPem);
        var revokedEntries = crl.GetRevokedCertificates();

        if (revokedEntries != null && revokedEntries.Count > 0)
        {
            var validSerial = new Org.BouncyCastle.Math.BigInteger(record.SerialNumber, 16);
            foreach (var entry in revokedEntries)
            {
                Assert.False(entry.SerialNumber.Equals(validSerial),
                    $"CRL should not contain non-revoked serial {record.SerialNumber}");
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
