using Content.Shared.Popups;

namespace Content.Shared._Onyx.Teleportation;

public sealed partial class BlockTeleportSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlockTeleportComponent, TeleportAttemptEvent>(OnAttempt);
    }

    private void OnAttempt(Entity<BlockTeleportComponent> ent, ref TeleportAttemptEvent args)
    {
        args.Cancelled = true;

        if (args.Message != null)
            _popup.PopupEntity(Loc.GetString(args.Message), ent, ent);
    }
}
