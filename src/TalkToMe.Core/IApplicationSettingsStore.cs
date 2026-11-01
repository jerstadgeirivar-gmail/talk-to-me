namespace TalkToMe.Core;

public interface IApplicationSettingsStore
{
    string? VocabularyFilePath => null;

    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken);

    async Task<string> LoadVocabularyAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).TechnicalVocabulary;

    async Task EnsureVocabularyAsync(CancellationToken cancellationToken) =>
        _ = await LoadVocabularyAsync(cancellationToken);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken);
}
