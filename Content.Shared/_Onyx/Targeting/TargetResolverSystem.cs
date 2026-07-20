using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Shared._Onyx.Targeting;

public sealed partial class TargetResolverSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public bool TryResolve(EntityUid body, EntityUid origin, out EntityUid part)
    {
        part = default;
        if (!_configuration.GetCVar(CCVars.TargetingEnabled) || !TryComp(origin, out TargetingComponent? targeting) ||
            !SharedTargetingSystem.IsSelectable(targeting.Target))
            return false;

        return TryResolve(body, targeting.Target, targeting, out part);
    }

    public bool TryResolve(EntityUid body, TargetBodyPart requested, EntityUid? shooter, out EntityUid part)
    {
        TryComp(shooter, out TargetingComponent? targeting);
        return TryResolve(body, requested, targeting, out part);
    }

    private bool TryResolve(EntityUid body, TargetBodyPart requested, TargetingComponent? targeting, out EntityUid part)
    {
        part = default;
        if (!_configuration.GetCVar(CCVars.TargetingEnabled) || !SharedTargetingSystem.IsSelectable(requested))
            return false;

        var target = requested;
        if (_configuration.GetCVar(CCVars.TargetingUseAnatomicalOdds) &&
            targeting != null &&
            !(_configuration.GetCVar(CCVars.TargetingDownedTargetsAreExact) && _standing.IsDown(body)))
            target = Roll(targeting, target);

        return TryResolveAvailable(body, target, out part);
    }

    public bool TryResolveAvailable(EntityUid body, TargetBodyPart target, out EntityUid part)
    {
        part = default;
        if (!SharedTargetingSystem.TryConvert(target, out var type, out var symmetry))
            return false;

        if (TryFind(body, type, symmetry, out part))
            return true;

        if (type == BodyPartType.Hand && TryFind(body, BodyPartType.Arm, symmetry, out part) ||
            type == BodyPartType.Foot && TryFind(body, BodyPartType.Leg, symmetry, out part))
            return true;

        return TryFind(body, BodyPartType.Chest, BodyPartSymmetry.None, out part) ||
               TryFind(body, BodyPartType.Torso, BodyPartSymmetry.None, out part);
    }

    public bool TryResolveExact(EntityUid body, TargetBodyPart target, out EntityUid part)
    {
        part = default;
        return SharedTargetingSystem.TryConvert(target, out var type, out var symmetry) &&
               TryFind(body, type, symmetry, out part);
    }

    public bool IsAvailable(EntityUid body, TargetBodyPart target) => TryResolveAvailable(body, target, out _);

    public List<EntityUid> GetMatchingParts(EntityUid body, TargetBodyPart mask)
    {
        var parts = new List<EntityUid>();
        var seen = new HashSet<EntityUid>();
        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            if (!SharedTargetingSystem.TryConvert(component.PartType, component.Symmetry, out var target))
                continue;

            var matches = (mask & target) != 0;
            if (matches && seen.Add(part))
                parts.Add(part);
        }

        return parts;
    }

    private TargetBodyPart Roll(TargetingComponent component, TargetBodyPart requested)
    {
        if (!component.TargetOdds.TryGetValue(requested, out var outcomes))
            return requested;

        var total = outcomes.Where(entry => SharedTargetingSystem.IsSelectable(entry.Key) && float.IsFinite(entry.Value) && entry.Value > 0f)
            .Sum(entry => entry.Value);
        if (!float.IsFinite(total) || total <= 0f)
            return requested;

        var roll = _random.NextFloat() * total;
        foreach (var (part, weight) in outcomes)
        {
            if (!SharedTargetingSystem.IsSelectable(part) || !float.IsFinite(weight) || weight <= 0f)
                continue;
            roll -= weight;
            if (roll <= 0f)
                return part;
        }
        return requested;
    }

    private bool TryFind(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid part)
    {
        foreach (var candidate in _body.GetBodyChildrenOfType(body, type))
        {
            if (type is not BodyPartType.Torso and not BodyPartType.Chest and not BodyPartType.Groin and not BodyPartType.Head && candidate.Component.Symmetry != symmetry)
                continue;
            part = candidate.Id;
            return true;
        }
        part = default;
        return false;
    }
}
