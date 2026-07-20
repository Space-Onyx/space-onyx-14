using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class OrganDamageSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private AmputationSystem _amputation = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WoundableComponent, PartDamageAppliedEvent>(OnPartDamageApplied);
    }

    private void OnPartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        _fractures.HandlePartDamageApplied(part, ref args);
        _amputation.HandlePartDamageApplied(part, ref args);

        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            !_prototypes.TryIndex(part.Comp.Profile, out var profile))
            return;

        var chance = profile.OrganDamageChances.GetValueOrDefault(bodyPart.PartType);
        if (chance <= 0f || !_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        var organs = _body.GetPartOrgans(part).Where(organ => organ.Component.Health > FixedPoint2.Zero).ToList();
        if (organs.Count == 0)
            return;

        var organ = PickOrgan(organs, profile);
        var damage = GetOrganDamage(args.Damage, profile);
        if (damage <= FixedPoint2.Zero)
            return;

        organ.Component.Health = FixedPoint2.Clamp(organ.Component.Health - damage, FixedPoint2.Zero, organ.Component.MaxHealth);
        Dirty(organ.Id, organ.Component);
        var ev = new OrganDamageAppliedEvent(args.Body, part.Owner, organ.Id, damage);
        RaiseLocalEvent(organ.Id, ref ev);
    }

    private static FixedPoint2 GetOrganDamage(DamageSpecifier damage, WoundableProfilePrototype profile)
    {
        var result = FixedPoint2.Zero;
        foreach (var (type, amount) in damage.DamageDict)
            result += amount * profile.OrganDamageMultipliers.GetValueOrDefault(type, 0f);
        return result;
    }

    private (EntityUid Id, OrganComponent Component) PickOrgan(
        List<(EntityUid Id, OrganComponent Component)> organs,
        WoundableProfilePrototype profile)
    {
        var totalWeight = 0f;
        foreach (var organ in organs)
            totalWeight += GetWeight(profile, organ.Component);

        if (totalWeight <= 0f)
            return _random.Pick(organs);

        var roll = _random.NextFloat() * totalWeight;
        foreach (var organ in organs)
        {
            roll -= GetWeight(profile, organ.Component);
            if (roll <= 0f)
                return organ;
        }
        return organs[organs.Count - 1];
    }

    private static float GetWeight(WoundableProfilePrototype profile, OrganComponent organ)
    {
        return organ.Category is { } category && profile.OrganDamageWeights.TryGetValue(category, out var weight)
            ? Math.Max(0f, weight)
            : 1f;
    }
}

[ByRefEvent]
public readonly record struct OrganDamageAppliedEvent(
    EntityUid Body,
    EntityUid Part,
    EntityUid Organ,
    FixedPoint2 Damage);
