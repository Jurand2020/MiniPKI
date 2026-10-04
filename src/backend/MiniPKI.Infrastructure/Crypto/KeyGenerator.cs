using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace MiniPKI.Infrastructure.Crypto;

/// <summary>
/// Generates key pairs (RSA, ECDSA P-256) and random serial numbers.
/// </summary>
public static class KeyGenerator
{
    /// <summary>
    /// Generates an RSA key pair of the specified size.
    /// </summary>
    public static AsymmetricCipherKeyPair GenerateRsaKeyPair(int keySize)
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), keySize));
        return generator.GenerateKeyPair();
    }

    /// <summary>
    /// Generates an ECDSA key pair on the P-256 (secp256r1) curve.
    /// </summary>
    public static AsymmetricCipherKeyPair GenerateEcdsaKeyPair()
    {
        var generator = new ECKeyPairGenerator();
        var curve = CustomNamedCurves.GetByName("P-256");
        var domainParams = new ECDomainParameters(curve);
        generator.Init(new ECKeyGenerationParameters(domainParams, new SecureRandom()));
        return generator.GenerateKeyPair();
    }

    /// <summary>
    /// Generates a random 128-bit positive serial number as an uppercase hex string.
    /// </summary>
    public static string GenerateSerialNumber()
    {
        var random = new SecureRandom();
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new BigInteger(1, bytes).ToString(16).ToUpperInvariant();
    }

    /// <summary>
    /// Returns the signature algorithm name for the given key type.
    /// </summary>
    public static string GetSignatureAlgorithm(AsymmetricKeyParameter key)
    {
        return key is RsaKeyParameters ? "SHA256withRSA" : "SHA256withECDSA";
    }
}
