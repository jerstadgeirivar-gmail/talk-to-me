namespace VoiceType.Infrastructure;

public interface IClipboardAdapter
{
    object? CaptureData();

    void SetText(string text);

    bool ContainsText(string text);

    void RestoreData(object? data);
}
