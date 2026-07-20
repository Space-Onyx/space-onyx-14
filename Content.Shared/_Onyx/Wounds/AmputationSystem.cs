using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Throwing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class AmputationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;

    internal void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType is BodyPartType.Torso or BodyPartType.Chest || bodyPart.Parent == null ||
            !_prototypes.TryIndex(part.Comp.Profile, out var profile) ||
            !profile.AmputationThresholds.TryGetValue(bodyPart.PartType, out var thresholds))
            return;

        var parent = bodyPart.Parent.Value;
        if (!TryComp(part, out DamageableComponent? damageable) ||
            !ReachedThreshold(_damageable.GetAllDamage((part.Owner, damageable)), thresholds))
            return;

        if (!_body.TryDetachPart(part.Owner))
            return;

        Dirty(part.Owner, part.Comp);
        _throwing.TryThrow(part.Owner, _random.NextVector2(0.8f, 1.2f), baseThrowSpeed: 3f,
            pushbackRatio: 0f, doSpin: true);
        var ev = new PartAmputatedEvent(args.Body, part.Owner, parent);
        RaiseLocalEvent(part.Owner, ref ev);
    }

    internal static bool ReachedThreshold(
        DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> thresholds)
    {
        var progress = 0f;
        foreach (var (type, threshold) in thresholds)
        {
            if (threshold <= FixedPoint2.Zero)
                continue;
            progress += (float) damage.DamageDict.GetValueOrDefault(type) / (float) threshold;
        }
        return progress >= 1f;
    }
}

[ByRefEvent]
public readonly record struct PartAmputatedEvent(EntityUid Body, EntityUid Part, EntityUid Parent);
