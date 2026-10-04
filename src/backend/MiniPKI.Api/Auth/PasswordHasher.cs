using System.Security.Cryptography;
using System.Text;

namespace MiniPKI.Api.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher.
/// Hash format: pbkdf2-sha256:{iterations}:{base64-salt}:{base64-hash}
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 100_000;

    /// <summary>
    /// Hashes a plaintext password using PBKDF2-HMAC-SHA256.
    /// </summary>
    public static string Hash(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2-sha256:{iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a plaintext password against a stored hash.
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 4) return false;
        if (parts[0] != "pbkdf2-sha256") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
