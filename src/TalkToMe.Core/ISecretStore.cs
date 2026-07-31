namespace TalkToMe.Core;

public interface ISecretStore
{
    Task<bool> HasSecretAsync(CancellationToken cancellationToken);

    Task<string?> GetSecretAsync(CancellationToken cancellationToken);

    Task SetSecretAsync(string secret, CancellationToken cancellationToken);

    Task RemoveSecretAsync(CancellationToken cancellationToken);
}

public interface INamedSecretStore : ISecretStore
{
    Task<bool> HasSecretAsync(string providerId, CancellationToken cancellationToken);
    Task<string?> GetSecretAsync(string providerId, CancellationToken cancellationToken);
    Task SetSecretAsync(string providerId, string secret, CancellationToken cancellationToken);
    Task RemoveSecretAsync(string providerId, CancellationToken cancellationToken);
}
