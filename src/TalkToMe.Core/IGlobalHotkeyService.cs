namespace TalkToMe.Core;

public interface IGlobalHotkeyService : IDisposable
{
    string GestureText { get; }

    int HotkeyId { get; }

    int WindowMessage { get; }

    bool Register(nint windowHandle);

    bool Reconfigure(string hotkey);

    void Unregister();
}
