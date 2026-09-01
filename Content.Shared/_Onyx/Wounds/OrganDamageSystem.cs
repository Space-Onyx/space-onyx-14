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
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private AmputationSystem _amputation = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WoundableComponent, PartDamageAppliedEvent>(OnPartDamageApplied);
    }

    private void OnPartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        _wounds.HandlePartDamageApplied(part, ref args);
        _fractures.HandlePartDamageApplied(part, ref args);
        _amputation.HandlePartDamageApplied(part, ref args);
        _bleeding.HandlePartDamageApplied(part, ref args);

        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            !_prototypes.TryIndex(part.Comp.Profile, out var profile))
            return;

        var settings = profile.OrganDamage;
        var chance = settings.Chances.GetValueOrDefault(bodyPart.PartType);
        if (chance <= 0f || !_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        var organs = _body.GetPartOrgans(part).Where(organ => organ.Component.Health > FixedPoint2.Zero).ToList();
        if (organs.Count == 0)
            return;

        var affected = Math.Max(1, settings.MaxAffected);
        for (var i = 0; i < affected && organs.Count > 0; i++)
        {
            var organ = PickOrgan(organs, settings);
            organs.Remove(organ);

            if (!_random.Prob(Math.Clamp(organ.Component.DamageChanceMultiplier, 0f, 1f)))
                continue;

            var applied = GetOrganDamage(args.Damage, settings, organ.Component);
            if (applied <= FixedPoint2.Zero)
                continue;

            if (settings.MaxDamageFraction > 0f)
                applied = FixedPoint2.Min(applied, organ.Component.MaxHealth * settings.MaxDamageFraction);

            organ.Component.Health = FixedPoint2.Clamp(organ.Component.Health - applied, FixedPoint2.Zero, organ.Component.MaxHealth);
            Dirty(organ.Id, organ.Component);
        }
    }

    private static FixedPoint2 GetOrganDamage(
        DamageSpecifier damage,
        OrganDamageSettings settings,
        OrganComponent organ)
    {
        var result = FixedPoint2.Zero;
        foreach (var (type, amount) in damage.DamageDict)
        {
            var baseline = settings.DamageMultipliers.GetValueOrDefault(type, 0f);
            var vulnerability = organ.DamageMultipliers.GetValueOrDefault(type, 1f);
            result += amount * baseline * Math.Max(0f, vulnerability);
        }
        return result;
    }

    private (EntityUid Id, OrganComponent Component) PickOrgan(
        List<(EntityUid Id, OrganComponent Component)> organs,
        OrganDamageSettings settings)
    {
        var totalWeight = 0f;
        foreach (var organ in organs)
            totalWeight += GetWeight(settings, organ.Component);

        if (totalWeight <= 0f)
            return _random.Pick(organs);

        var roll = _random.NextFloat() * totalWeight;
        foreach (var organ in organs)
        {
            roll -= GetWeight(settings, organ.Component);
            if (roll <= 0f)
                return organ;
        }
        return organs[organs.Count - 1];
    }

    private static float GetWeight(OrganDamageSettings settings, OrganComponent organ)
    {
        var baseline = organ.Category is { } category && settings.Weights.TryGetValue(category, out var weight)
            ? Math.Max(0f, weight)
            : 1f;
        return baseline * Math.Max(0f, organ.SelectionMultiplier);
    }
}
