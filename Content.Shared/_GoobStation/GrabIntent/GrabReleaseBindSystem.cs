using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.GrabIntent;

public sealed partial class GrabReleaseBindSystem : EntitySystem
{
    [Dependency] private GrabIntentSystem _grab = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ResistGrab, InputCmdHandler.FromDelegate(Resist, handle: false))
            .Register<GrabReleaseBindSystem>();
    }

    private void Resist(ICommonSession? session)
    {
        if (session?.AttachedEntity is { } uid && TryComp<GrabbableComponent>(uid, out var grabbable))
            _grab.TryResist((uid, grabbable));
    }
}
