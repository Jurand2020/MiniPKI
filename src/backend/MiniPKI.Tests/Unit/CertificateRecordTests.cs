using MiniPKI.Core.Domain;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Tests.Unit;

public class CertificateRecordTests
{
    [Fact]
    public void Status_ReturnsValid_WhenNotRevokedOrExpired()
    {
        var record = new CertificateRecord
        {
            SerialNumber = "ABC123",
            CommonName = "test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        Assert.Equal(CertificateStatus.Valid, record.Status);
    }

    [Fact]
    public void Status_ReturnsRevoked_WhenRevokedAtIsSet()
    {
        var record = new CertificateRecord
        {
            SerialNumber = "ABC123",
            CommonName = "test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            RevokedAt = DateTime.UtcNow.AddDays(-1)
        };

        Assert.Equal(CertificateStatus.Revoked, record.Status);
    }

    [Fact]
    public void Status_ReturnsExpired_WhenExpiresAtIsInPast()
    {
        var record = new CertificateRecord
        {
            SerialNumber = "ABC123",
            CommonName = "test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        Assert.Equal(CertificateStatus.Expired, record.Status);
    }

    [Fact]
    public void Status_ReturnsRevoked_WhenBothRevokedAndExpired()
    {
        var record = new CertificateRecord
        {
            SerialNumber = "ABC123",
            CommonName = "test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            RevokedAt = DateTime.UtcNow.AddDays(-5)
        };

        Assert.Equal(CertificateStatus.Revoked, record.Status);
    }
}

public class DataPathProviderTests
{
    [Fact]
    public void Constructor_SetsBasePath()
    {
        var provider = new DataPathProvider("/tmp/pki");

        Assert.Equal("/tmp/pki", provider.BasePath);
    }

    [Fact]
    public void GetCertificatePath_ReturnsCorrectPath()
    {
        var provider = new DataPathProvider("/tmp/pki");

        var path = provider.GetCertificatePath("ABC123");

        Assert.Equal(Path.Combine("/tmp/pki", "certificates", "ABC123.crt"), path);
    }

    [Fact]
    public void GetKeyPath_ReturnsCorrectPath()
    {
        var provider = new DataPathProvider("/tmp/pki");

        var path = provider.GetKeyPath("ABC123");

        Assert.Equal(Path.Combine("/tmp/pki", "certificates", "ABC123.key"), path);
    }

    [Fact]
    public void GetMetadataPath_ReturnsCorrectPath()
    {
        var provider = new DataPathProvider("/tmp/pki");

        var path = provider.GetMetadataPath("ABC123");

        Assert.Equal(Path.Combine("/tmp/pki", "certificates", "ABC123.json"), path);
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesAllDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pki-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var provider = new DataPathProvider(tempDir);
            provider.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(provider.CaPath));
            Assert.True(Directory.Exists(provider.CertificatesPath));
            Assert.True(Directory.Exists(provider.RevokedPath));
            Assert.True(Directory.Exists(provider.CrlPath));
            Assert.True(Directory.Exists(provider.AuditPath));
            Assert.True(Directory.Exists(provider.ConfigurationPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
