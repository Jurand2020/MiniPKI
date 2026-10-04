using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Integration;

public class FullPkiWorkflowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _config;
    private readonly FileConfigurationStore _configStore;
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _store;
    private readonly FileAuditService _audit;
    private readonly BouncyCastleCertificateService _certService;
    private readonly BouncyCastleCrlService _crlService;

    public FullPkiWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pki-e2e-test-" + Guid.NewGuid().ToString("N")[..8]);
        _paths = new DataPathProvider(_tempDir);
        _paths.EnsureDirectoriesExist();
        _config = new PkiConfiguration { RsaKeySize = 2048, CrlUrl = "http://crl.test.com" };
        _configStore = new FileConfigurationStore(_paths, _config);
        _caService = new BouncyCastleCertificateAuthorityService(_paths, _config, _configStore);
        _store = new FileCertificateStore(_paths);
        _audit = new FileAuditService(_paths);
        _crlService = new BouncyCastleCrlService(_caService, _store, _audit, _paths);
        _certService = new BouncyCastleCertificateService(_caService, _store, _audit, _config, _configStore, _crlService);
    }

    [Fact]
    public async Task FullWorkflow_IssueRevokeGenerateCrl()
    {
        // 1. Initialize CA
        await _caService.EnsureCaInitializedAsync();
        Assert.True(File.Exists(_paths.RootCertPath));
        Assert.True(File.Exists(_paths.IntermediateCertPath));

        // 2. Issue a certificate
        var cert1 = await _certService.IssueAsync(
            "server1.test.com",
            new List<string> { "server1.test.com", "www.server1.test.com" },
            KeyAlgorithm.RSA, 2048, 365);

        Assert.Equal(CertificateStatus.Valid, cert1.Status);
        Assert.Single(await _certService.ListAsync());

        // 3. Issue another certificate
        var cert2 = await _certService.IssueAsync(
            "server2.test.com",
            new List<string> { "server2.test.com" },
            KeyAlgorithm.RSA, 2048, 365);

        Assert.Equal(2, (await _certService.ListAsync()).Count);

        // 4. Revoke the first certificate
        var revokeResult = await _certService.RevokeAsync(cert1.SerialNumber, "keyCompromise");
        Assert.True(revokeResult);

        // 5. Generate CRL (should contain the revoked cert)
        var crlPem = await _crlService.GenerateCrlAsync();
        Assert.Contains("-----BEGIN X509 CRL-----", crlPem);

        // 6. Verify audit log has entries
        var auditEntries = await _audit.GetEntriesAsync();
        Assert.True(auditEntries.Count >= 3); // At least CA init, issue, revoke
    }

    [Fact]
    public async Task FullWorkflow_DownloadReturnsValidPem()
    {
        await _caService.EnsureCaInitializedAsync();

        var cert = await _certService.IssueAsync(
            "download.test.com",
            new List<string> { "download.test.com" },
            KeyAlgorithm.RSA, 2048, 365);

        var (certPem, keyPem, chainPem) = await _certService.DownloadAsync(cert.SerialNumber);

        Assert.Contains("-----BEGIN CERTIFICATE-----", certPem);
        Assert.Contains("-----BEGIN PRIVATE KEY-----", keyPem);

        // Chain should contain 2 certificates (intermediate + root)
        var chainCertCount = chainPem.Split("-----BEGIN CERTIFICATE-----").Length - 1;
        Assert.Equal(2, chainCertCount);
    }

    [Fact]
    public async Task FullWorkflow_CaChainIsValid()
    {
        await _caService.EnsureCaInitializedAsync();

        var rootPem = await _caService.GetRootCaPemAsync();
        var intPem = await _caService.GetIntermediateCaPemAsync();

        var rootCert = PemHelper.ReadCertificatePem(rootPem);
        var intCert = PemHelper.ReadCertificatePem(intPem);

        // Root should be self-signed (issuer == subject)
        Assert.Equal(rootCert.SubjectDN, rootCert.IssuerDN);

        // Intermediate should be signed by root
        Assert.Equal(rootCert.SubjectDN, intCert.IssuerDN);
    }

    [Fact]
    public async Task FullWorkflow_EcdsaCertificateIssuance()
    {
        await _caService.EnsureCaInitializedAsync();

        var cert = await _certService.IssueAsync(
            "ecdsa.test.com",
            new List<string> { "ecdsa.test.com" },
            KeyAlgorithm.ECDSA, 256, 365);

        Assert.Equal(KeyAlgorithm.ECDSA, cert.KeyAlgorithm);
        Assert.Equal(CertificateStatus.Valid, cert.Status);

        // Verify the certificate file exists and is readable
        var (certPem, _, _) = await _certService.DownloadAsync(cert.SerialNumber);
        Assert.Contains("-----BEGIN CERTIFICATE-----", certPem);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
