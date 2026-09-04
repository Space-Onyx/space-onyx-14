using Content.Shared._Onyx.Communications;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Communications;

public sealed partial class CommunicationsConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StationCommunicationsConsoleComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<StationCommunicationsConsoleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<StationCommunicationsConsoleBoundUserInterface>(ent.Owner, StationCommunicationsConsoleUi.Key, out var bui))
            bui.Update(ent);
    }
}
