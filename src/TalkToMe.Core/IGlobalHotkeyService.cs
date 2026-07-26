namespace TalkToMe.Core;

public interface IGlobalHotkeyService : IDisposable
{
    int HotkeyId { get; }

    int WindowMessage { get; }

    bool Register(nint windowHandle);

    void Unregister();
}
