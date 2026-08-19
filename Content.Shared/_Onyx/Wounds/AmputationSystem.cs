using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Throwing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class AmputationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundDamageRoutingSystem _damageRouting = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;

    public const string AmputationConsequenceWound = "AmputationConsequenceWound";

    private static readonly FixedPoint2 AmputationConsequenceSeverity = 35;
    private static readonly FixedPoint2 AmputationConsequenceBlunt = 15;
    private static readonly FixedPoint2 AmputationConsequenceSlash = 20;

    public void ApplyAmputationConsequences(EntityUid body, EntityUid parent)
    {
        if (!_net.IsServer || !HasComp<WoundableComponent>(parent))
            return;

        _wounds.CreateOrMergeWound(parent, AmputationConsequenceWound, AmputationConsequenceSeverity);
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = AmputationConsequenceBlunt,
                ["Slash"] = AmputationConsequenceSlash,
            }
        };
        _damageRouting.TryApplyPartDamage(body, parent, damage);
    }

    public void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer)
            return;

        var healed = FixedPoint2.Zero;
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount < FixedPoint2.Zero && (type == "Blunt" || type == "Slash"))
                healed += -amount;
        }

        if (healed > FixedPoint2.Zero)
        {
            foreach (var wound in _wounds.GetWounds((part.Owner, (WoundableComponent?) part.Comp)).ToArray())
            {
                if (wound.Comp.Prototype == AmputationConsequenceWound)
                    _wounds.ChangeSeverity(new Entity<WoundComponent?>(wound.Owner, wound.Comp), -healed);
            }
        }

        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType is BodyPartType.Chest || bodyPart.Parent == null ||
            !_prototypes.TryIndex(part.Comp.Profile, out var profile) ||
            !profile.AmputationThresholds.TryGetValue(bodyPart.PartType, out var thresholds) ||
            !args.Damage.DamageDict.Any(entry => entry.Value > FixedPoint2.Zero && thresholds.ContainsKey(entry.Key)))
            return;

        var parent = bodyPart.Parent.Value;
        if (!TryComp(part, out DamageableComponent? damageable) ||
            !ReachedThreshold(_damageable.GetAllDamage((part.Owner, damageable)), thresholds))
            return;

        if (!_body.TryDetachPart(part.Owner))
            return;

        _wounds.CreateOrMergeWound(parent, "DismembermentWound", GetDismembermentSeverity(bodyPart.PartType));
        ApplyAmputationConsequences(args.Body, parent);
        _throwing.TryThrow(part.Owner, _random.NextVector2(0.8f, 1.2f), baseThrowSpeed: 3f,
            pushbackRatio: 0f, doSpin: true);
        var ev = new PartAmputatedEvent(args.Body, part.Owner, parent);
        RaiseLocalEvent(part.Owner, ref ev);
    }

    private static bool ReachedThreshold(
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

    private static FixedPoint2 GetDismembermentSeverity(BodyPartType type) => type switch
    {
        BodyPartType.Head => 200,
        BodyPartType.Groin => 160,
        BodyPartType.Arm or BodyPartType.Leg => 120,
        BodyPartType.Hand or BodyPartType.Foot => 80,
        _ => 100,
    };
}

[ByRefEvent]
public readonly record struct PartAmputatedEvent(EntityUid Body, EntityUid Part, EntityUid Parent);
