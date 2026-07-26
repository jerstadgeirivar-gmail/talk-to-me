namespace TalkToMe.Core;

public interface IWindowTargetService
{
    WindowTarget CaptureForegroundTarget();

    bool IsValid(WindowTarget target);

    Task<bool> ActivateAsync(WindowTarget target, CancellationToken cancellationToken);
}
