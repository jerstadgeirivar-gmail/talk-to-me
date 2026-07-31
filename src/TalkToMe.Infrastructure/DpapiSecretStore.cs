using System.Security.Cryptography;
using System.Text;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class DpapiSecretStore(string? secretFile = null) : INamedSecretStore
{
    private readonly string _secretFile = secretFile ?? TalkToMeDataPaths.ProtectedSecretFile;
    private readonly string _namedDirectory = secretFile is null
        ? TalkToMeDataPaths.ProtectedSecretsDirectory
        : Path.Combine(Path.GetDirectoryName(secretFile)!, "Secrets");

    public Task<bool> HasSecretAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(_secretFile));
    }

    public async Task<string?> GetSecretAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_secretFile))
        {
            return null;
        }

        byte[] protectedBytes = await File.ReadAllBytesAsync(
            _secretFile,
            cancellationToken);
        byte[] plainBytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public async Task SetSecretAsync(string secret, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        byte[] plainBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(_secretFile)!);
            string temporaryPath = _secretFile + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken);
            File.Move(temporaryPath, _secretFile, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public Task RemoveSecretAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(_secretFile);
        return Task.CompletedTask;
    }

    public Task<bool> HasSecretAsync(string providerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetNamedPath(providerId)) ||
            providerId == TranscriptionProviderIds.AzureOpenAi && File.Exists(_secretFile));
    }

    public async Task<string?> GetSecretAsync(string providerId, CancellationToken cancellationToken)
    {
        string path = GetNamedPath(providerId);
        if (!File.Exists(path) && providerId == TranscriptionProviderIds.AzureOpenAi && File.Exists(_secretFile))
        {
            string? legacy = await GetSecretAsync(cancellationToken);
            if (!string.IsNullOrEmpty(legacy))
            {
                await SetSecretAsync(providerId, legacy, cancellationToken);
            }
            return legacy;
        }

        return await ReadSecretAsync(path, cancellationToken);
    }

    public Task SetSecretAsync(string providerId, string secret, CancellationToken cancellationToken) =>
        WriteSecretAsync(GetNamedPath(providerId), secret, cancellationToken);

    public Task RemoveSecretAsync(string providerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetNamedPath(providerId));
        if (providerId == TranscriptionProviderIds.AzureOpenAi)
        {
            File.Delete(_secretFile);
        }
        return Task.CompletedTask;
    }

    private string GetNamedPath(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Provider identifiers may contain only letters, digits, hyphens, and underscores.", nameof(providerId));
        }
        return Path.Combine(_namedDirectory, providerId + ".bin");
    }

    private static async Task<string?> ReadSecretAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        byte[] protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(plainBytes); }
        finally { CryptographicOperations.ZeroMemory(plainBytes); }
    }

    private static async Task WriteSecretAsync(string path, string secret, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        byte[] plainBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(plainBytes); }
    }
}
