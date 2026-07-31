using System.Windows;

namespace TalkToMe.Infrastructure;

public sealed class WpfClipboardAdapter : IClipboardAdapter
{
    public object? CaptureData()
    {
        IDataObject? current = Clipboard.GetDataObject();
        if (current is null)
        {
            return null;
        }

        DataObject snapshot = new();
        foreach (string format in current.GetFormats(autoConvert: false))
        {
            object? value = current.GetData(format, autoConvert: false);
            if (value is not null)
            {
                snapshot.SetData(format, value, autoConvert: false);
            }
        }

        return snapshot;
    }

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
