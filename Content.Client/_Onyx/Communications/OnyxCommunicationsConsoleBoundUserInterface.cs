using Content.Shared._Onyx.Communications;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Communications;

[UsedImplicitly]
public sealed class OnyxCommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OnyxCommunicationsConsoleMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<OnyxCommunicationsConsoleMenu>();
        _menu.OnMessage += SendMessage;
        if (EntMan.TryGetComponent<OnyxCommunicationsConsoleComponent>(Owner, out var console))
            Update((Owner, console));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (EntMan.TryGetComponent<OnyxCommunicationsConsoleComponent>(Owner, out var console))
            Update((Owner, console));
    }

    public void Update(Entity<OnyxCommunicationsConsoleComponent> console)
    {
        _menu?.Update(console);
    }
}
