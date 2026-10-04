using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace MiniPKI.Infrastructure.Crypto;

/// <summary>
/// PEM encoding/decoding helpers for certificates, private keys, and CRLs.
/// </summary>
public static class PemHelper
{
    public static string WriteCertificatePem(X509Certificate cert)
    {
        using var sw = new StringWriter();
        var pemWriter = new PemWriter(sw);
        pemWriter.WriteObject(cert);
        return sw.ToString();
    }

    public static string WritePrivateKeyPem(AsymmetricKeyParameter privateKey)
    {
        var info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
        var pemObj = new Org.BouncyCastle.Utilities.IO.Pem.PemObject("PRIVATE KEY", info.GetEncoded());
        using var sw = new StringWriter();
        var pemWriter = new PemWriter(sw);
        pemWriter.WriteObject(pemObj);
        return sw.ToString();
    }

    public static string WriteCrlPem(X509Crl crl)
    {
        using var sw = new StringWriter();
        var pemWriter = new PemWriter(sw);
        pemWriter.WriteObject(crl);
        return sw.ToString();
    }

    public static X509Certificate ReadCertificatePem(string pem)
    {
        using var sr = new StringReader(pem);
        var pemReader = new PemReader(sr);
        var obj = pemReader.ReadObject();
        if (obj is X509Certificate cert)
            return cert;
        throw new InvalidOperationException("PEM is not a certificate");
    }

    public static X509Crl ReadCrlPem(string pem)
    {
        using var sr = new StringReader(pem);
        var pemReader = new PemReader(sr);
        var obj = pemReader.ReadObject();
        if (obj is X509Crl crl)
            return crl;
        throw new InvalidOperationException("PEM is not a CRL");
    }

    public static AsymmetricKeyParameter ReadPrivateKeyPem(string pem)
    {
        using var sr = new StringReader(pem);
        var pemReader = new PemReader(sr);
        var obj = pemReader.ReadObject();
        if (obj is AsymmetricKeyParameter key)
            return key;
        if (obj is AsymmetricCipherKeyPair pair)
            return pair.Private;
        throw new InvalidOperationException("PEM is not a private key");
    }

    public static X509Certificate ConvertToBouncyCastle(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
    {
        var raw = cert.RawData;
        var parser = new X509CertificateParser();
        return parser.ReadCertificate(raw);
    }

    public static AsymmetricKeyParameter ConvertToBouncyCastlePrivateKey(System.Security.Cryptography.RSA rsa)
    {
        var parameters = rsa.ExportParameters(true);
        var rsaParams = new RsaPrivateCrtKeyParameters(
            new BigInteger(1, parameters.Modulus),
            new BigInteger(1, parameters.Exponent),
            new BigInteger(1, parameters.D),
            new BigInteger(1, parameters.P),
            new BigInteger(1, parameters.Q),
            new BigInteger(1, parameters.DP),
            new BigInteger(1, parameters.DQ),
            new BigInteger(1, parameters.InverseQ));
        return rsaParams;
    }

    public static string WriteDerAsPem(byte[] derData, string type)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {type}-----");
        var base64 = Convert.ToBase64String(derData);
        for (var i = 0; i < base64.Length; i += 64)
        {
            var len = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, len));
        }
        sb.AppendLine($"-----END {type}-----");
        return sb.ToString();
    }

    /// <summary>
    /// Creates a PKCS#12 (PFX) archive containing the certificate, private key, and CA chain.
    /// </summary>
    public static byte[] WritePkcs12(
        X509Certificate cert,
        AsymmetricKeyParameter privateKey,
        X509Certificate[] chain,
        string password)
    {
        var store = new Pkcs12StoreBuilder().Build();
        var certEntry = new X509CertificateEntry(cert);
        var friendlyName = cert.SubjectDN.GetValueList(X509Name.CN).Count > 0
            ? (string)cert.SubjectDN.GetValueList(X509Name.CN)[0]
            : "certificate";

        store.SetCertificateEntry(friendlyName, certEntry);
        store.SetKeyEntry(friendlyName, new AsymmetricKeyEntry(privateKey), new[] { certEntry });

        // Add CA chain certificates
        for (var i = 0; i < chain.Length; i++)
        {
            var alias = $"ca-{i}";
            if (store.GetCertificate(alias) == null)
            {
                store.SetCertificateEntry(alias, new X509CertificateEntry(chain[i]));
            }
        }

        using var ms = new MemoryStream();
        store.Save(ms, password.ToCharArray(), new SecureRandom());
        return ms.ToArray();
    }

    /// <summary>
    /// Parses a PEM-encoded Certificate Signing Request (CSR).
    /// </summary>
    public static Pkcs10CertificationRequest ReadCsrPem(string pem)
    {
        using var sr = new StringReader(pem);
        var pemReader = new PemReader(sr);
        var obj = pemReader.ReadObject();
        if (obj is Pkcs10CertificationRequest csr)
            return csr;
        throw new InvalidOperationException("PEM is not a CSR");
    }
}
