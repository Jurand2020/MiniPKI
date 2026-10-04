using MiniPKI.Infrastructure.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

namespace MiniPKI.Tests.Unit;

public class KeyGeneratorTests
{
    [Fact]
    public void GenerateRsaKeyPair_ReturnsValidKeyPair()
    {
        var keyPair = KeyGenerator.GenerateRsaKeyPair(2048);

        Assert.NotNull(keyPair);
        Assert.IsType<RsaKeyParameters>(keyPair.Public);
        Assert.IsType<RsaPrivateCrtKeyParameters>(keyPair.Private);
    }

    [Fact]
    public void GenerateRsaKeyPair_HasCorrectKeySize()
    {
        var keyPair = KeyGenerator.GenerateRsaKeyPair(2048);
        var rsaPublic = (RsaKeyParameters)keyPair.Public;

        var modulus = rsaPublic.Modulus;
        Assert.Equal(2048, modulus.BitLength);
    }

    [Fact]
    public void GenerateEcdsaKeyPair_ReturnsValidKeyPair()
    {
        var keyPair = KeyGenerator.GenerateEcdsaKeyPair();

        Assert.NotNull(keyPair);
        Assert.NotNull(keyPair.Public);
        Assert.NotNull(keyPair.Private);
    }

    [Fact]
    public void GenerateSerialNumber_ReturnsNonEmptyHexString()
    {
        var serial = KeyGenerator.GenerateSerialNumber();

        Assert.False(string.IsNullOrEmpty(serial));
        Assert.Matches("^[0-9A-F]+$", serial);
    }

    [Fact]
    public void GenerateSerialNumber_ReturnsUniqueValues()
    {
        var serial1 = KeyGenerator.GenerateSerialNumber();
        var serial2 = KeyGenerator.GenerateSerialNumber();

        Assert.NotEqual(serial1, serial2);
    }

    [Fact]
    public void GenerateSerialNumber_HasSufficientLength()
    {
        var serial = KeyGenerator.GenerateSerialNumber();

        // 128-bit serial = 32 hex chars
        Assert.True(serial.Length >= 32, $"Serial length {serial.Length} is less than 32");
    }

    [Fact]
    public void GetSignatureAlgorithm_ReturnsCorrectAlgorithmForRsa()
    {
        var keyPair = KeyGenerator.GenerateRsaKeyPair(2048);
        var alg = KeyGenerator.GetSignatureAlgorithm(keyPair.Private);

        Assert.Equal("SHA256withRSA", alg);
    }

    [Fact]
    public void GetSignatureAlgorithm_ReturnsCorrectAlgorithmForEcdsa()
    {
        var keyPair = KeyGenerator.GenerateEcdsaKeyPair();
        var alg = KeyGenerator.GetSignatureAlgorithm(keyPair.Private);

        Assert.Equal("SHA256withECDSA", alg);
    }
}
