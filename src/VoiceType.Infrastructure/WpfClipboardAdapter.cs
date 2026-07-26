using System.Windows;

namespace VoiceType.Infrastructure;

public sealed class WpfClipboardAdapter : IClipboardAdapter
{
    public object? CaptureData() => Clipboard.GetDataObject();

    public void SetText(string text) => Clipboard.SetText(text, TextDataFormat.UnicodeText);

    public bool ContainsText(string text) =>
        Clipboard.ContainsText(TextDataFormat.UnicodeText) &&
        Clipboard.GetText(TextDataFormat.UnicodeText) == text;

    public void RestoreData(object? data)
    {
        if (data is IDataObject dataObject)
        {
            Clipboard.SetDataObject(dataObject, copy: true);
        }
        else
        {
            Clipboard.Clear();
        }
    }
}
