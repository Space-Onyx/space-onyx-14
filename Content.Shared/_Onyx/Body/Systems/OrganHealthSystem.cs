using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Mobs;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Body.Systems;

public sealed partial class OrganHealthSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<OrganComponent>();
        while (query.MoveNext(out var uid, out var organ))
        {
            if (organ.Health > FixedPoint2.Zero)
                continue;

            if (HasComp<BrainComponent>(uid))
            {
                if (organ.Body is { } body &&
                    TryComp(body, out MobStateComponent? mobState) &&
                    !_mobState.IsDead(body, mobState) &&
                    _mobState.HasState(body, MobState.Dead, mobState))
                    _mobState.ChangeMobState(body, MobState.Dead, mobState, uid);

                continue;
            }

            DestroyOrgan((uid, organ));
        }
    }

    private void DestroyOrgan(Entity<OrganComponent> organ)
    {
        var parent = Transform(organ).ParentUid;
        if (TryComp<BodyPartComponent>(parent, out var part))
        {
            foreach (var slot in part.Organs)
            {
                if (!_body.TryGetOrganInSlot(parent, slot, out var slotted) || slotted != organ.Owner)
                    continue;

                if (_body.TryRemoveOrgan(parent, slot, out var removed))
                {
                    if (organ.Comp.DestructionWound is { } wound &&
                        organ.Comp.DestructionWoundSeverity > FixedPoint2.Zero &&
                        HasComp<WoundableComponent>(parent))
                        _wounds.CreateOrMergeWound(parent, wound, organ.Comp.DestructionWoundSeverity);

                    QueueDel(removed);
                    return;
                }

                break;
            }
        }

        QueueDel(organ);
    }
}
