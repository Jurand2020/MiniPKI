using MiniPKI.Api.Auth;
using MiniPKI.Infrastructure.Storage;

namespace MiniPKI.Api.Auth;

/// <summary>
/// Manages admin password: load from file, change, first-run setup.
/// </summary>
public class PasswordService
{
    private readonly DataPathProvider _paths;
    private readonly IConfiguration _config;
    private string? _cachedHash;

    public PasswordService(DataPathProvider paths, IConfiguration config)
    {
        _paths = paths;
        _config = config;
    }

    /// <summary>
    /// Returns the current password hash. Loads from file if available,
    /// otherwise falls back to configuration (Auth:PasswordHash).
    /// </summary>
    public string GetPasswordHash()
    {
        if (_cachedHash != null) return _cachedHash;

        if (File.Exists(_paths.PasswordFilePath))
        {
            _cachedHash = File.ReadAllText(_paths.PasswordFilePath).Trim();
        }
        else
        {
            _cachedHash = _config["Auth:PasswordHash"] ?? string.Empty;
        }

        return _cachedHash;
    }

    /// <summary>
    /// Returns true if the password is still the default (first run).
    /// </summary>
    public bool IsFirstRun()
    {
        return !File.Exists(_paths.PasswordFilePath);
    }

    /// <summary>
    /// Changes the admin password. Returns true on success.
    /// </summary>
    public bool ChangePassword(string currentPassword, string newPassword)
    {
        var currentHash = GetPasswordHash();
        if (!PasswordHasher.Verify(currentPassword, currentHash))
            return false;

        var newHash = PasswordHasher.Hash(newPassword);
        File.WriteAllText(_paths.PasswordFilePath, newHash);
        _cachedHash = newHash;

        return true;
    }

    /// <summary>
    /// Sets the initial password on first run.
    /// </summary>
    public bool SetInitialPassword(string newPassword)
    {
        if (!IsFirstRun()) return false;

        var newHash = PasswordHasher.Hash(newPassword);
        File.WriteAllText(_paths.PasswordFilePath, newHash);
        _cachedHash = newHash;

        return true;
    }
}
