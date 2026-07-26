namespace TalkToMe.Core;

public interface ISecretStore
{
    Task<bool> HasSecretAsync(CancellationToken cancellationToken);

    Task<string?> GetSecretAsync(CancellationToken cancellationToken);

    Task SetSecretAsync(string secret, CancellationToken cancellationToken);

    Task RemoveSecretAsync(CancellationToken cancellationToken);
}
