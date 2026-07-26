using System.Security.Cryptography;
using System.Text;
using VoiceType.Core;

namespace VoiceType.Infrastructure;

public sealed class DpapiSecretStore(string? secretFile = null) : ISecretStore
{
    private readonly string _secretFile = secretFile ?? VoiceTypeDataPaths.ProtectedSecretFile;

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
}
