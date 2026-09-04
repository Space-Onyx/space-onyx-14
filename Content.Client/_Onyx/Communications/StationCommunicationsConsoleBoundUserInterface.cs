using Content.Shared._Onyx.Communications;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Communications;

[UsedImplicitly]
public sealed class StationCommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private StationCommunicationsConsoleMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<StationCommunicationsConsoleMenu>();
        _menu.OnMessage += SendMessage;
        if (EntMan.TryGetComponent<StationCommunicationsConsoleComponent>(Owner, out var console))
            Update((Owner, console));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (EntMan.TryGetComponent<StationCommunicationsConsoleComponent>(Owner, out var console))
            Update((Owner, console));
    }

    public void Update(Entity<StationCommunicationsConsoleComponent> console)
    {
        _menu?.Update(console);
    }
}
