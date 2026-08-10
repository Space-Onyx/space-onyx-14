using Content.Shared._Onyx.Communications;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Communications;

public sealed partial class CommunicationsConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OnyxCommunicationsConsoleComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<OnyxCommunicationsConsoleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<OnyxCommunicationsConsoleBoundUserInterface>(ent.Owner, OnyxCommunicationsConsoleUi.Key, out var bui))
            bui.Update(ent);
    }
}
