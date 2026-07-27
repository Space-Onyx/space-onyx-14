using Content.Shared._Onyx.Salvage.Weapons;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Salvage.Weapons;

public sealed partial class BlockChargeSystem : SharedBlockChargeSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<BlockChargeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.HasCharge || _timing.CurTime < comp.NextCharge)
                continue;

            comp.HasCharge = true;
            if (Transform(uid).ParentUid is { } parent && HasComp<BlockChargeUserComponent>(parent))
                _popup.PopupEntity(Loc.GetString("block-charge-startup", ("entity", uid)), parent, parent);
            Dirty(uid, comp);
        }
    }
}
