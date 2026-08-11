using Content.Shared._Onyx.CustomLawboard;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.CustomLawboard;

public sealed partial class CustomLawboardSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CustomLawboardComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CustomLawboardComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<CustomLawboardBoundInterface>(ent.Owner, CustomLawboardUiKey.Key, out var bui))
            bui.Update(ent);
    }
}
