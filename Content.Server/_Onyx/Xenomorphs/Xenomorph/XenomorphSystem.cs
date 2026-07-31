using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Xenomorphs.Xenomorph;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenomorphs.Xenomorph;

public sealed partial class XenomorphSystem : SharedXenomorphSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XenomorphComponent>();
        while (query.MoveNext(out var uid, out var xenomorph))
        {
            if (!xenomorph.OnWeed || xenomorph.WeedHeal == null || _timing.CurTime < xenomorph.NextPointsAt)
                continue;

            xenomorph.NextPointsAt = _timing.CurTime + xenomorph.WeedHealRate;
            _damage.TryChangeDamage(uid, xenomorph.WeedHeal);

            foreach (var (part, _) in _body.GetBodyChildren(uid))
            {
                if (!TryComp<WoundableComponent>(part, out var woundable))
                    continue;

                foreach (var wound in _wounds.GetWounds((part, woundable)))
                    _bleeding.ReduceBleeding(wound.Owner, FixedPoint2.New(0.5f));
            }
        }
    }
}
