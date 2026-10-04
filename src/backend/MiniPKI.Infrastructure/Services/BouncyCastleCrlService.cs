using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.X509;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Crypto;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Infrastructure.Services;

/// <summary>
/// BouncyCastle-based CRL service.
/// Generates and publishes Certificate Revocation Lists (CRLs).
/// </summary>
public class BouncyCastleCrlService : ICrlService
{
    private readonly BouncyCastleCertificateAuthorityService _caService;
    private readonly FileCertificateStore _store;
    private readonly FileAuditService _audit;
    private readonly DataPathProvider _paths;

    public BouncyCastleCrlService(
        BouncyCastleCertificateAuthorityService caService,
        FileCertificateStore store,
        FileAuditService audit,
        DataPathProvider paths)
    {
        _caService = caService;
        _store = store;
        _audit = audit;
        _paths = paths;
    }

    /// <summary>
    /// Generates a new CRL containing all revoked certificates.
    /// Writes the CRL to the CRL directory as ca.crl.
    /// </summary>
    public async Task<string> GenerateCrlAsync()
    {
        var issuerCert = _caService.GetIntermediateCertificate();
        var issuerKey = _caService.GetIntermediatePrivateKey();

        var crlGen = new X509V2CrlGenerator();
        crlGen.SetIssuerDN(issuerCert.SubjectDN);
        crlGen.SetThisUpdate(DateTime.UtcNow);
        crlGen.SetNextUpdate(DateTime.UtcNow.AddDays(1));

        // Add revoked certificates
        var revoked = await _store.ListRevokedAsync();
        foreach (var record in revoked)
        {
            if (record.RevokedAt == null) continue;
            crlGen.AddCrlEntry(
                new Org.BouncyCastle.Math.BigInteger(record.SerialNumber, 16),
                record.RevokedAt.Value,
                0);
        }

        // Generate and sign CRL
        var sigAlg = KeyGenerator.GetSignatureAlgorithm(issuerKey);
        var sigFactory = new Asn1SignatureFactory(sigAlg, issuerKey);
        var crl = crlGen.Generate(sigFactory);

        // Write to file
        var crlPem = PemHelper.WriteCrlPem(crl);
        await File.WriteAllTextAsync(_paths.CrlFilePath, crlPem);

        // Audit log
        await _audit.LogAsync("CRL_GENERATE",
            $"Generated CRL with {revoked.Count} revoked certificates");

        return crlPem;
    }

    /// <summary>
    /// Returns the current CRL in PEM format (generates if not exists).
    /// </summary>
    public async Task<string> GetCurrentCrlPemAsync()
    {
        if (File.Exists(_paths.CrlFilePath))
        {
            return await File.ReadAllTextAsync(_paths.CrlFilePath);
        }
        return await GenerateCrlAsync();
    }

    /// <summary>
    /// Returns the list of revoked certificate records.
    /// </summary>
    public Task<List<Core.Domain.CertificateRecord>> GetRevokedCertificatesAsync()
    {
        return _store.ListRevokedAsync();
    }
}
