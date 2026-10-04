using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using MiniPKI.Core.Configuration;
using MiniPKI.Core.Domain;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Infrastructure.Services;

/// <summary>
/// BouncyCastle-based certificate service.
/// Issues, revokes, lists, and retrieves end-entity certificates.
/// </summary>
public class BouncyCastleCertificateService : ICertificateService
{
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _store;
    private readonly FileAuditService _audit;
    private readonly PkiConfiguration _config;
    private readonly FileConfigurationStore _configStore;
    private readonly BouncyCastleCrlService _crlService;

    public BouncyCastleCertificateService(
        BouncyCastleCertificateAuthorityService caService,
        FileCertificateStore store,
        FileAuditService audit,
        PkiConfiguration config,
        FileConfigurationStore configStore,
        BouncyCastleCrlService crlService)
    {
        _caService = caService;
        _store = store;
        _audit = audit;
        _config = config;
        _configStore = configStore;
        _crlService = crlService;
    }

    /// <summary>
    /// Lists all issued certificates (valid and revoked).
    /// </summary>
    public Task<List<CertificateRecord>> ListAsync()
    {
        return _store.ListAsync();
    }

    /// <summary>
    /// Retrieves a certificate record by serial number.
    /// </summary>
    public Task<CertificateRecord?> GetBySerialAsync(string serialNumber)
    {
        return _store.GetBySerialAsync(serialNumber);
    }

    /// <summary>
    /// Issues a new end-entity certificate signed by the Intermediate CA.
    /// </summary>
    public async Task<CertificateRecord> IssueAsync(string commonName, List<string> sanEntries,
        KeyAlgorithm algorithm, int keySize, int validityDays)
    {
        // Load runtime configuration
        var runtimeConfig = await _configStore.LoadAsync();

        // Generate key pair based on algorithm
        AsymmetricCipherKeyPair keyPair;
        if (algorithm == KeyAlgorithm.RSA)
        {
            keyPair = KeyGenerator.GenerateRsaKeyPair(keySize);
        }
        else // ECDSA
        {
            keyPair = KeyGenerator.GenerateEcdsaKeyPair();
        }

        // Generate serial number
        var serial = KeyGenerator.GenerateSerialNumber();

        // Get CA objects
        var issuerCert = _caService.GetIntermediateCertificate();
        var issuerKey = _caService.GetIntermediatePrivateKey();

        // Build CRL distribution point URL from runtime config
        var crlUrl = BuildCrlUrl(runtimeConfig);

        // Generate certificate
        var cert = CertificateGenerator.GenerateEndEntityCertificate(
            keyPair, serial, commonName,
            runtimeConfig.Organization, runtimeConfig.OrganizationalUnit,
            runtimeConfig.Country, runtimeConfig.State, runtimeConfig.Locality,
            sanEntries, validityDays, crlUrl, issuerCert, issuerKey);

        // Convert to PEM
        var certPem = PemHelper.WriteCertificatePem(cert);
        var keyPem = PemHelper.WritePrivateKeyPem(keyPair.Private);

        // Create record
        var record = new CertificateRecord
        {
            SerialNumber = serial,
            CommonName = commonName,
            SanEntries = sanEntries,
            KeyAlgorithm = algorithm,
            KeySize = algorithm == KeyAlgorithm.RSA ? keySize : 256,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(validityDays),
            Issuer = "MiniPKI Intermediate CA"
        };

        // Save to store
        await _store.SaveAsync(record, certPem, keyPem);

        // Audit log
        await _audit.LogAsync("ISSUE",
            $"Issued certificate for {commonName} (serial: {serial})", serial);

        return record;
    }

    /// <summary>
    /// Revokes a certificate by serial number and regenerates the CRL.
    /// </summary>
    public async Task<bool> RevokeAsync(string serialNumber, string reason)
    {
        var record = await _store.GetBySerialAsync(serialNumber);
        if (record == null) return false;
        if (record.Status == CertificateStatus.Revoked) return false;

        var result = await _store.RevokeAsync(serialNumber, reason);
        if (result)
        {
            await _audit.LogAsync("REVOKE",
                $"Revoked certificate {serialNumber}: {reason}", serialNumber);

            // Regenerate CRL to include the newly revoked certificate
            await _crlService.GenerateCrlAsync();
        }

        return result;
    }

    /// <summary>
    /// Downloads the certificate, private key, and CA chain for the given serial.
    /// </summary>
    public async Task<(string certificatePem, string keyPem, string chainPem)> DownloadAsync(string serialNumber)
    {
        var certPem = await _store.GetCertificatePemAsync(serialNumber);
        var keyPem = await _store.GetPrivateKeyPemAsync(serialNumber);
        var chainPem = await _caService.GetCaChainPemAsync();
        return (certPem, keyPem, chainPem);
    }

    /// <summary>
    /// Issues a certificate from a CSR (Certificate Signing Request).
    /// The private key is NOT stored since it belongs to the requester.
    /// </summary>
    public async Task<CertificateRecord> IssueFromCsrAsync(string csrPem, int validityDays)
    {
        // Load runtime configuration
        var runtimeConfig = await _configStore.LoadAsync();

        var csr = PemHelper.ReadCsrPem(csrPem);
        var publicKey = csr.GetPublicKey();
        var subject = csr.GetCertificationRequestInfo().Subject;
        var commonName = subject.GetValueList(X509Name.CN).Count > 0
            ? (string)subject.GetValueList(X509Name.CN)[0]
            : "unknown";

        // Extract SANs from CSR attributes if present
        var sanEntries = new List<string>();
        var requestedExtensions = csr.GetRequestedExtensions();
        if (requestedExtensions != null)
        {
            var sanExt = requestedExtensions.GetExtension(X509Extensions.SubjectAlternativeName);
            if (sanExt != null)
            {
                var san = GeneralNames.GetInstance(
                    Asn1Object.FromByteArray(sanExt.Value.GetOctets()));
                foreach (var name in san.GetNames())
                {
                    if (name.TagNo == GeneralName.DnsName)
                    {
                        var nameStr = name.Name.ToString();
                        if (!string.IsNullOrEmpty(nameStr))
                            sanEntries.Add(nameStr);
                    }
                }
            }
        }

        var serial = KeyGenerator.GenerateSerialNumber();
        var issuerCert = _caService.GetIntermediateCertificate();
        var issuerKey = _caService.GetIntermediatePrivateKey();
        var crlUrl = BuildCrlUrl(runtimeConfig);

        var cert = CertificateGenerator.GenerateEndEntityCertificateFromCsr(
            publicKey, serial, subject, sanEntries, validityDays,
            crlUrl, issuerCert, issuerKey);

        var certPem = PemHelper.WriteCertificatePem(cert);

        // For CSR-based issuance, we don't have the private key
        var record = new CertificateRecord
        {
            SerialNumber = serial,
            CommonName = commonName,
            SanEntries = sanEntries,
            KeyAlgorithm = KeyAlgorithm.RSA, // CSR key type
            KeySize = 2048,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(validityDays),
            Issuer = "MiniPKI Intermediate CA"
        };

        await _store.SaveAsync(record, certPem, string.Empty);

        await _audit.LogAsync("ISSUE_CSR",
            $"Issued certificate from CSR for {commonName} (serial: {serial})", serial);

        return record;
    }

    /// <summary>
    /// Resolves the CRL distribution point URL from configuration.
    /// The {API_CRL} placeholder is replaced with "/api/crl/current".
    /// Returns empty string if CrlUrl is empty.
    /// </summary>
    private static string BuildCrlUrl(PkiConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.CrlUrl))
            return string.Empty;

        return config.CrlUrl.Replace("{API_CRL}", "/api/crl/current");
    }
}
