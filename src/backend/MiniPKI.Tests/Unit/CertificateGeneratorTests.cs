using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Crypto;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;

namespace MiniPKI.Tests.Unit;

public class CertificateGeneratorTests
{
    private const string Cn = "test.example.com";
    private const string Org = "TestOrg";
    private const string Ou = "IT Department";
    private const string Country = "PL";
    private const string State = "Greater Poland";
    private const string Locality = "Poznan";
    private const string CrlUrl = "http://crl.example.com/ca.crl";

    [Fact]
    public void GenerateRootCaCertificate_ReturnsValidCertificate()
    {
        var keyPair = KeyGenerator.GenerateRsaKeyPair(2048);

        var cert = CertificateGenerator.GenerateRootCaCertificate(
            keyPair, "Test Root CA", Org, Ou, Country, State, Locality);

        Assert.NotNull(cert);
        Assert.Equal("CN=Test Root CA,O=TestOrg,OU=IT Department,L=Poznan,ST=Greater Poland,C=PL", cert.SubjectDN.ToString());
        Assert.Equal(cert.SubjectDN, cert.IssuerDN);
    }

    [Fact]
    public void GenerateRootCaCertificate_HasBasicConstraintsCaTrue()
    {
        var keyPair = KeyGenerator.GenerateRsaKeyPair(2048);

        var cert = CertificateGenerator.GenerateRootCaCertificate(
            keyPair, "Root", Org, Ou, Country, State, Locality);

        var ext = cert.GetExtensionValue(X509Extensions.BasicConstraints);
        var bc = BasicConstraints.GetInstance(Asn1Object.FromByteArray(ext.GetOctets()));
        Assert.True(bc.IsCA());
    }

    [Fact]
    public void GenerateIntermediateCaCertificate_ReturnsValidCertificate()
    {
        var rootKeyPair = KeyGenerator.GenerateRsaKeyPair(2048);
        var intKeyPair = KeyGenerator.GenerateRsaKeyPair(2048);

        var rootCert = CertificateGenerator.GenerateRootCaCertificate(
            rootKeyPair, "Root", Org, Ou, Country, State, Locality);

        var intCert = CertificateGenerator.GenerateIntermediateCaCertificate(
            intKeyPair, rootCert, rootKeyPair.Private, "Intermediate", Org, Ou, Country, State, Locality);

        Assert.NotNull(intCert);
        Assert.Equal(rootCert.SubjectDN, intCert.IssuerDN);
    }

    [Fact]
    public void GenerateEndEntityCertificate_ReturnsValidCertificate()
    {
        var (eeCert, intCert, _) = GenerateEndEntity();

        Assert.NotNull(eeCert);
        Assert.Equal(intCert.SubjectDN, eeCert.IssuerDN);
    }

    [Fact]
    public void GenerateEndEntityCertificate_ContainsLocalityAndOu()
    {
        var (eeCert, _, _) = GenerateEndEntity();

        var subjectDn = eeCert.SubjectDN.ToString();

        // Verify OU is present
        Assert.Contains("OU=IT Department", subjectDn);

        // Verify L (locality) is present
        Assert.Contains("L=Poznan", subjectDn);

        // Verify other fields
        Assert.Contains("CN=test.example.com", subjectDn);
        Assert.Contains("O=TestOrg", subjectDn);
        Assert.Contains("C=PL", subjectDn);
        Assert.Contains("ST=Greater Poland", subjectDn);
    }

    [Fact]
    public void GenerateEndEntityCertificate_HasSanExtension()
    {
        var (eeCert, _, _) = GenerateEndEntity();

        var sanExt = eeCert.GetExtensionValue(X509Extensions.SubjectAlternativeName);
        Assert.NotNull(sanExt);
    }

    [Fact]
    public void GenerateEndEntityCertificate_HasCrlDistributionPoint()
    {
        var (eeCert, _, _) = GenerateEndEntity();

        var crlDpExt = eeCert.GetExtensionValue(X509Extensions.CrlDistributionPoints);
        Assert.NotNull(crlDpExt);
    }

    [Fact]
    public void GenerateEndEntityCertificate_NoCrlDistributionPoint_WhenUrlIsEmpty()
    {
        var (eeCert, _, _) = GenerateEndEntity(crlUrl: string.Empty);

        var crlDpExt = eeCert.GetExtensionValue(X509Extensions.CrlDistributionPoints);
        Assert.Null(crlDpExt);
    }

    private static (X509Certificate eeCert, X509Certificate intCert, X509Certificate rootCert) GenerateEndEntity(
        string crlUrl = CrlUrl)
    {
        var rootKeyPair = KeyGenerator.GenerateRsaKeyPair(2048);
        var intKeyPair = KeyGenerator.GenerateRsaKeyPair(2048);
        var eeKeyPair = KeyGenerator.GenerateRsaKeyPair(2048);

        var rootCert = CertificateGenerator.GenerateRootCaCertificate(
            rootKeyPair, "Root", Org, Ou, Country, State, Locality);
        var intCert = CertificateGenerator.GenerateIntermediateCaCertificate(
            intKeyPair, rootCert, rootKeyPair.Private, "Intermediate", Org, Ou, Country, State, Locality);

        var serial = KeyGenerator.GenerateSerialNumber();
        var eeCert = CertificateGenerator.GenerateEndEntityCertificate(
            eeKeyPair, serial, Cn, Org, Ou, Country, State, Locality,
            new List<string> { "test.example.com", "alt.example.com" },
            365, crlUrl, intCert, intKeyPair.Private);

        return (eeCert, intCert, rootCert);
    }
}
