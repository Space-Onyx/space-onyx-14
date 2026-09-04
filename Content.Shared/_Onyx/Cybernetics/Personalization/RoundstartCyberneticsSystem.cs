using System.Linq;
using Content.Shared._Onyx.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Cybernetics.Personalization;

public sealed partial class RoundstartCyberneticsSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public bool TryApply(EntityUid body, HumanoidCharacterProfile profile)
    {
        if (!_configuration.GetCVar(CCVars.RoundstartCyberneticsEnabled))
            return true;

        if (!_prototypes.TryIndex(profile.Species, out SpeciesPrototype? species))
            return false;

        var normalized = RoundstartCyberneticsResolver.Normalize(
            profile.Cybernetics,
            species.RoundstartCyberwareCapacity,
            _prototypes,
            EntityManager.ComponentFactory);
        if (!normalized.SequenceEqual(profile.Cybernetics))
            return false;

        if (!RoundstartCyberneticsResolver.TryResolve(
                normalized,
                _prototypes,
                EntityManager.ComponentFactory,
                out var resolved,
                out _))
            return false;

        if (resolved.Count == 0)
            return true;

        var existing = new Dictionary<(BodyPartType, BodyPartSymmetry), (EntityUid Id, BodyPartComponent Part)>();
        foreach (var part in _body.GetBodyChildren(body))
        {
            if (part.Component.PartType is not (BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot))
                continue;

            if (!existing.TryAdd((part.Component.PartType, part.Component.Symmetry), part))
                return false;
        }

        var replacements = new Dictionary<(BodyPartType, BodyPartSymmetry), EntityUid>();
        var spawned = new List<EntityUid>();
        foreach (var id in resolved)
        {
            var replacement = Spawn(id, Transform(body).Coordinates);
            spawned.Add(replacement);
            if (!TryComp(replacement, out BodyPartComponent? part) ||
                !replacements.TryAdd((part.PartType, part.Symmetry), replacement) ||
                !existing.ContainsKey((part.PartType, part.Symmetry)))
            {
                DeleteSpawned();
                return false;
            }
        }

        var ordered = replacements.Keys
            .OrderBy(key => key.Item1 is BodyPartType.Arm or BodyPartType.Leg ? 0 : 1)
            .ToList();
        var applied = new List<((BodyPartType, BodyPartSymmetry) Key, EntityUid Old, EntityUid Replacement, EntityUid Parent)>();
        EnsureComp<BodyPartReplacementComponent>(body);

        foreach (var key in ordered)
        {
            var old = existing[key];
            if (old.Part.Parent is not { } oldParent)
            {
                Rollback();
                RemComp<BodyPartReplacementComponent>(body);
                return false;
            }

            var parentKey = Comp<BodyPartComponent>(oldParent);
            var parent = replacements.GetValueOrDefault((parentKey.PartType, parentKey.Symmetry), oldParent);
            var replacement = replacements[key];
            if (!_body.AreTransplantsCompatible(parent, replacement) ||
                !_body.TryDetachPart(old.Id, reparent: false) ||
                !_body.TryAttachPart(parent, replacement))
            {
                if (Comp<BodyPartComponent>(old.Id).Parent == null)
                    _body.TryAttachPart(oldParent, old.Id);
                Rollback();
                RemComp<BodyPartReplacementComponent>(body);
                return false;
            }

            applied.Add((key, old.Id, replacement, oldParent));
        }

        foreach (var operation in applied)
            QueueDel(operation.Old);
        RemComp<BodyPartReplacementComponent>(body);
        return true;

        bool Rollback()
        {
            var success = true;
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                var operation = applied[i];
                if (!_body.TryDetachPart(operation.Replacement, reparent: false) || !_body.TryAttachPart(operation.Parent, operation.Old))
                {
                    success = false;
                    Log.Error($"Failed to restore body part {ToPrettyString(operation.Old)} while applying round-start cybernetics to {ToPrettyString(body)}");
                }
            }

            DeleteSpawned();
            return success;
        }

        void DeleteSpawned()
        {
            foreach (var entity in spawned)
            {
                if (Exists(entity) &&
                    (!TryComp(entity, out BodyPartComponent? part) || part.Body == null))
                    QueueDel(entity);
            }
        }
    }
}
