using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Infrastructure.Services;

/// <summary>
/// BouncyCastle-based Certificate Authority service.
/// Manages Root CA and Intermediate CA lifecycle (creation, loading, retrieval).
/// </summary>
public class BouncyCastleCertificateAuthorityService : ICertificateAuthorityService
{
    private readonly DataPathProvider _paths;
    private readonly PkiConfiguration _config;
    private readonly FileConfigurationStore _configStore;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private X509Certificate? _rootCert;
    private AsymmetricKeyParameter? _rootPrivateKey;
    private X509Certificate? _intermediateCert;
    private AsymmetricKeyParameter? _intermediatePrivateKey;

    public BouncyCastleCertificateAuthorityService(
        DataPathProvider paths,
        PkiConfiguration config,
        FileConfigurationStore configStore)
    {
        _paths = paths;
        _config = config;
        _configStore = configStore;
    }

    /// <summary>
    /// Initializes the CA: loads existing CA files or generates new ones.
    /// </summary>
    public async Task EnsureCaInitializedAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_paths.RootCertPath) && File.Exists(_paths.RootKeyPath) &&
                File.Exists(_paths.IntermediateCertPath) && File.Exists(_paths.IntermediateKeyPath))
            {
                // Load existing CA
                var rootCertPem = await File.ReadAllTextAsync(_paths.RootCertPath);
                var rootKeyPem = await File.ReadAllTextAsync(_paths.RootKeyPath);
                _rootCert = PemHelper.ReadCertificatePem(rootCertPem);
                _rootPrivateKey = PemHelper.ReadPrivateKeyPem(rootKeyPem);

                var intCertPem = await File.ReadAllTextAsync(_paths.IntermediateCertPath);
                var intKeyPem = await File.ReadAllTextAsync(_paths.IntermediateKeyPath);
                _intermediateCert = PemHelper.ReadCertificatePem(intCertPem);
                _intermediatePrivateKey = PemHelper.ReadPrivateKeyPem(intKeyPem);
            }
            else
            {
                await GenerateCaAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Generates Root CA and Intermediate CA certificates and keys.
    /// Uses runtime configuration from FileConfigurationStore.
    /// </summary>
    private async Task GenerateCaAsync()
    {
        // Load runtime configuration
        var runtimeConfig = await _configStore.LoadAsync();

        // Root CA
        var rootKeyPair = KeyGenerator.GenerateRsaKeyPair(runtimeConfig.RsaKeySize);
        var rootCert = CertificateGenerator.GenerateRootCaCertificate(
            rootKeyPair, "MiniPKI Root CA",
            runtimeConfig.Organization, runtimeConfig.OrganizationalUnit,
            runtimeConfig.Country, runtimeConfig.State, runtimeConfig.Locality);

        var rootCertPem = PemHelper.WriteCertificatePem(rootCert);
        var rootKeyPem = PemHelper.WritePrivateKeyPem(rootKeyPair.Private);

        await File.WriteAllTextAsync(_paths.RootCertPath, rootCertPem);
        await File.WriteAllTextAsync(_paths.RootKeyPath, rootKeyPem);

        _rootCert = rootCert;
        _rootPrivateKey = rootKeyPair.Private;

        // Intermediate CA
        var intKeyPair = KeyGenerator.GenerateRsaKeyPair(runtimeConfig.RsaKeySize);
        var intCert = CertificateGenerator.GenerateIntermediateCaCertificate(
            intKeyPair, rootCert, rootKeyPair.Private,
            "MiniPKI Intermediate CA",
            runtimeConfig.Organization, runtimeConfig.OrganizationalUnit,
            runtimeConfig.Country, runtimeConfig.State, runtimeConfig.Locality);

        var intCertPem = PemHelper.WriteCertificatePem(intCert);
        var intKeyPem = PemHelper.WritePrivateKeyPem(intKeyPair.Private);

        await File.WriteAllTextAsync(_paths.IntermediateCertPath, intCertPem);
        await File.WriteAllTextAsync(_paths.IntermediateKeyPath, intKeyPem);

        _intermediateCert = intCert;
        _intermediatePrivateKey = intKeyPair.Private;
    }

    /// <summary>
    /// Returns the PEM-encoded Root CA certificate.
    /// </summary>
    public Task<string> GetRootCaPemAsync()
    {
        if (_rootCert == null) throw new InvalidOperationException("CA not initialized");
        return Task.FromResult(PemHelper.WriteCertificatePem(_rootCert));
    }

    /// <summary>
    /// Returns the PEM-encoded Intermediate CA certificate.
    /// </summary>
    public Task<string> GetIntermediateCaPemAsync()
    {
        if (_intermediateCert == null) throw new InvalidOperationException("CA not initialized");
        return Task.FromResult(PemHelper.WriteCertificatePem(_intermediateCert));
    }

    /// <summary>
    /// Returns the PEM-encoded Intermediate CA private key.
    /// </summary>
    public Task<string> GetIntermediateCaKeyPemAsync()
    {
        if (_intermediatePrivateKey == null) throw new InvalidOperationException("CA not initialized");
        return Task.FromResult(PemHelper.WritePrivateKeyPem(_intermediatePrivateKey));
    }

    /// <summary>
    /// Returns the full CA chain (intermediate + root) in PEM format.
    /// </summary>
    public Task<string> GetCaChainPemAsync()
    {
        if (_intermediateCert == null || _rootCert == null)
            throw new InvalidOperationException("CA not initialized");
        var chain = PemHelper.WriteCertificatePem(_intermediateCert) +
                    PemHelper.WriteCertificatePem(_rootCert);
        return Task.FromResult(chain);
    }

    /// <summary>
    /// Returns the BouncyCastle X509Certificate object for the intermediate CA.
    /// </summary>
    public X509Certificate GetIntermediateCertificate()
    {
        if (_intermediateCert == null) throw new InvalidOperationException("CA not initialized");
        return _intermediateCert;
    }

    /// <summary>
    /// Returns the BouncyCastle private key for the intermediate CA.
    /// </summary>
    public AsymmetricKeyParameter GetIntermediatePrivateKey()
    {
        if (_intermediatePrivateKey == null) throw new InvalidOperationException("CA not initialized");
        return _intermediatePrivateKey;
    }

    /// <summary>
    /// Resets the CA: clears in-memory objects, deletes all CA files,
    /// and regenerates Root CA and Intermediate CA using current configuration.
    /// </summary>
    public async Task ResetCaAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Clear in-memory CA objects
            _rootCert = null;
            _rootPrivateKey = null;
            _intermediateCert = null;
            _intermediatePrivateKey = null;

            // Delete CA files
            if (File.Exists(_paths.RootCertPath)) File.Delete(_paths.RootCertPath);
            if (File.Exists(_paths.RootKeyPath)) File.Delete(_paths.RootKeyPath);
            if (File.Exists(_paths.IntermediateCertPath)) File.Delete(_paths.IntermediateCertPath);
            if (File.Exists(_paths.IntermediateKeyPath)) File.Delete(_paths.IntermediateKeyPath);

            // Regenerate CA
            await GenerateCaAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
}
