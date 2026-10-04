using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace MiniPKI.Infrastructure.Crypto;

/// <summary>
/// Builds X.509 v3 certificates for Root CA, Intermediate CA, and end-entity TLS.
/// </summary>
public static class CertificateGenerator
{
    /// <summary>
    /// Generates a self-signed Root CA certificate.
    /// </summary>
    public static X509Certificate GenerateRootCaCertificate(
        AsymmetricCipherKeyPair keyPair,
        string commonName,
        string organization,
        string organizationalUnit,
        string country,
        string state,
        string locality)
    {
        var subject = BuildSubjectName(commonName, organization, organizationalUnit, country, state, locality);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(BigInteger.One);
        gen.SetSubjectDN(subject);
        gen.SetIssuerDN(subject);
        gen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        gen.SetNotAfter(DateTime.UtcNow.AddYears(20));
        gen.SetPublicKey(keyPair.Public);

        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            CreateSubjectKeyIdentifier(keyPair.Public));

        return gen.Generate(new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private));
    }

    /// <summary>
    /// Generates an Intermediate CA certificate signed by the Root CA.
    /// </summary>
    public static X509Certificate GenerateIntermediateCaCertificate(
        AsymmetricCipherKeyPair keyPair,
        X509Certificate rootCert,
        AsymmetricKeyParameter rootPrivateKey,
        string commonName,
        string organization,
        string organizationalUnit,
        string country,
        string state,
        string locality)
    {
        var subject = BuildSubjectName(commonName, organization, organizationalUnit, country, state, locality);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(BigInteger.ValueOf(2));
        gen.SetSubjectDN(subject);
        gen.SetIssuerDN(rootCert.SubjectDN);
        gen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        gen.SetNotAfter(DateTime.UtcNow.AddYears(10));
        gen.SetPublicKey(keyPair.Public);

        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(0));
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            CreateSubjectKeyIdentifier(keyPair.Public));
        gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            CreateAuthorityKeyIdentifier(rootCert));

        return gen.Generate(new Asn1SignatureFactory("SHA256WithRSA", rootPrivateKey));
    }

    /// <summary>
    /// Generates an end-entity TLS certificate signed by the Intermediate CA.
    /// The subject DN includes CN, O, OU, C, ST, L from configuration.
    /// </summary>
    public static X509Certificate GenerateEndEntityCertificate(
        AsymmetricCipherKeyPair keyPair,
        string serialNumber,
        string commonName,
        string organization,
        string organizationalUnit,
        string country,
        string state,
        string locality,
        List<string> sanEntries,
        int validityDays,
        string crlDistributionPointUrl,
        X509Certificate issuerCert,
        AsymmetricKeyParameter issuerPrivateKey)
    {
        var subject = BuildEndEntitySubjectName(
            commonName, organization, organizationalUnit, country, state, locality);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(new BigInteger(serialNumber, 16));
        gen.SetSubjectDN(subject);
        gen.SetIssuerDN(issuerCert.SubjectDN);
        gen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        gen.SetNotAfter(DateTime.UtcNow.AddDays(validityDays));
        gen.SetPublicKey(keyPair.Public);

        // Basic Constraints: not a CA
        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));

        // Key Usage: digital signature + key encipherment (for TLS server certs)
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        // Extended Key Usage: server authentication
        gen.AddExtension(X509Extensions.ExtendedKeyUsage, false,
            new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));

        // Subject Alternative Names
        if (sanEntries.Count > 0)
        {
            var generalNames = sanEntries
                .Select(name => new GeneralName(GeneralName.DnsName, name))
                .ToArray();
            gen.AddExtension(X509Extensions.SubjectAlternativeName, false, new GeneralNames(generalNames));
        }

        // CRL Distribution Point (only if URL is provided)
        if (!string.IsNullOrEmpty(crlDistributionPointUrl))
        {
            var dpName = new DistributionPointName(
                GeneralNames.GetInstance(
                    new DerSequence(
                        new GeneralName(GeneralName.UniformResourceIdentifier, crlDistributionPointUrl))));
            var crlDp = new CrlDistPoint(new[] { new DistributionPoint(dpName, null, null) });
            gen.AddExtension(X509Extensions.CrlDistributionPoints, false, crlDp);
        }

        // Subject Key Identifier
        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            CreateSubjectKeyIdentifier(keyPair.Public));

        // Authority Key Identifier
        gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            CreateAuthorityKeyIdentifier(issuerCert));

        var sigAlg = KeyGenerator.GetSignatureAlgorithm(issuerPrivateKey);
        return gen.Generate(new Asn1SignatureFactory(sigAlg, issuerPrivateKey));
    }

    /// <summary>
    /// Builds a subject DN for end-entity certificates.
    /// Only includes non-empty fields.
    /// </summary>
    private static X509Name BuildEndEntitySubjectName(
        string commonName, string organization, string organizationalUnit,
        string country, string state, string locality)
    {
        var oids = new List<DerObjectIdentifier>();
        var values = new List<string>();

        if (!string.IsNullOrEmpty(commonName))
        {
            oids.Add(X509Name.CN);
            values.Add(commonName);
        }
        if (!string.IsNullOrEmpty(organization))
        {
            oids.Add(X509Name.O);
            values.Add(organization);
        }
        if (!string.IsNullOrEmpty(organizationalUnit))
        {
            oids.Add(X509Name.OU);
            values.Add(organizationalUnit);
        }
        if (!string.IsNullOrEmpty(locality))
        {
            oids.Add(X509Name.L);
            values.Add(locality);
        }
        if (!string.IsNullOrEmpty(state))
        {
            oids.Add(X509Name.ST);
            values.Add(state);
        }
        if (!string.IsNullOrEmpty(country))
        {
            oids.Add(X509Name.C);
            values.Add(country);
        }

        return new X509Name(oids, values);
    }

    /// <summary>
    /// Generates an end-entity TLS certificate from a CSR's public key and subject.
    /// </summary>
    public static X509Certificate GenerateEndEntityCertificateFromCsr(
        AsymmetricKeyParameter publicKey,
        string serialNumber,
        X509Name subject,
        List<string> sanEntries,
        int validityDays,
        string crlDistributionPointUrl,
        X509Certificate issuerCert,
        AsymmetricKeyParameter issuerPrivateKey)
    {
        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(new BigInteger(serialNumber, 16));
        gen.SetSubjectDN(subject);
        gen.SetIssuerDN(issuerCert.SubjectDN);
        gen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        gen.SetNotAfter(DateTime.UtcNow.AddDays(validityDays));
        gen.SetPublicKey(publicKey);

        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
        gen.AddExtension(X509Extensions.ExtendedKeyUsage, false,
            new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));

        if (sanEntries.Count > 0)
        {
            var generalNames = sanEntries
                .Select(name => new GeneralName(GeneralName.DnsName, name))
                .ToArray();
            gen.AddExtension(X509Extensions.SubjectAlternativeName, false, new GeneralNames(generalNames));
        }

        if (!string.IsNullOrEmpty(crlDistributionPointUrl))
        {
            var dpName = new DistributionPointName(
                GeneralNames.GetInstance(
                    new DerSequence(
                        new GeneralName(GeneralName.UniformResourceIdentifier, crlDistributionPointUrl))));
            var crlDp = new CrlDistPoint(new[] { new DistributionPoint(dpName, null, null) });
            gen.AddExtension(X509Extensions.CrlDistributionPoints, false, crlDp);
        }

        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            X509ExtensionUtilities.CreateSubjectKeyIdentifier(publicKey));
        gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            X509ExtensionUtilities.CreateAuthorityKeyIdentifier(issuerCert));

        var sigAlg = KeyGenerator.GetSignatureAlgorithm(issuerPrivateKey);
        return gen.Generate(new Asn1SignatureFactory(sigAlg, issuerPrivateKey));
    }

    /// <summary>
    /// Builds a subject DN for CA certificates.
    /// Only includes non-empty fields.
    /// </summary>
    private static X509Name BuildSubjectName(
        string commonName, string organization, string organizationalUnit,
        string country, string state, string locality)
    {
        var oids = new List<DerObjectIdentifier>();
        var values = new List<string>();

        if (!string.IsNullOrEmpty(commonName))
        {
            oids.Add(X509Name.CN);
            values.Add(commonName);
        }
        if (!string.IsNullOrEmpty(organization))
        {
            oids.Add(X509Name.O);
            values.Add(organization);
        }
        if (!string.IsNullOrEmpty(organizationalUnit))
        {
            oids.Add(X509Name.OU);
            values.Add(organizationalUnit);
        }
        if (!string.IsNullOrEmpty(locality))
        {
            oids.Add(X509Name.L);
            values.Add(locality);
        }
        if (!string.IsNullOrEmpty(state))
        {
            oids.Add(X509Name.ST);
            values.Add(state);
        }
        if (!string.IsNullOrEmpty(country))
        {
            oids.Add(X509Name.C);
            values.Add(country);
        }

        return new X509Name(oids, values);
    }

    private static SubjectKeyIdentifier CreateSubjectKeyIdentifier(AsymmetricKeyParameter publicKey)
    {
        return X509ExtensionUtilities.CreateSubjectKeyIdentifier(publicKey);
    }

    private static AuthorityKeyIdentifier CreateAuthorityKeyIdentifier(X509Certificate issuerCert)
    {
        return X509ExtensionUtilities.CreateAuthorityKeyIdentifier(issuerCert);
    }
}
