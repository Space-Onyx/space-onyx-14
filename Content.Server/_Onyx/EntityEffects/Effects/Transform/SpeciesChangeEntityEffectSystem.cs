using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._Onyx.EntityEffects.Effects.Transform;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Polymorph;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.EntityEffects.Effects.Transform;

public sealed partial class SpeciesChangeEntityEffectSystem
    : EntityEffectSystem<HumanoidProfileComponent, SpeciesChange>
{
    [Dependency] private PermanentSpeciesChangeSystem _species = default!;

    protected override void Effect(Entity<HumanoidProfileComponent> entity, ref EntityEffectEvent<SpeciesChange> args)
    {
        _species.TryChange(entity.Owner, args.Effect.Species);
    }
}

public sealed partial class RandomSpeciesChangeEntityEffectSystem
    : EntityEffectSystem<HumanoidProfileComponent, RandomSpeciesChange>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PermanentSpeciesChangeSystem _species = default!;

    protected override void Effect(Entity<HumanoidProfileComponent> entity, ref EntityEffectEvent<RandomSpeciesChange> args)
    {
        var effect = args.Effect;
        var candidates = _prototypes.EnumeratePrototypes<SpeciesPrototype>()
            .Where(species => (effect.Whitelist == null || effect.Whitelist.Contains(species.ID)) &&
                              !effect.Blacklist.Contains(species.ID) &&
                              _prototypes.HasIndex<EntityPrototype>(species.Prototype))
            .Select(species => (ProtoId<SpeciesPrototype>) species.ID)
            .ToList();

        if (candidates.Count != 0)
            _species.TryChange(entity.Owner, _random.Pick(candidates));
    }
}

public sealed partial class PermanentSpeciesChangeSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public EntityUid? TryChange(EntityUid uid, ProtoId<SpeciesPrototype> speciesId)
    {
        if (!_prototypes.TryIndex(speciesId, out var species) ||
            !_prototypes.HasIndex<EntityPrototype>(species.Prototype))
            return null;

        var configuration = new PolymorphConfiguration
        {
            Entity = species.Prototype,
            Forced = true,
            TransferDamage = true,
            TransferName = true,
            Inventory = PolymorphInventoryChange.Transfer,
            RevertOnCrit = false,
            RevertOnDeath = false,
            RevertOnDelete = false,
        };

        var transformed = _polymorph.PolymorphEntity(uid, configuration);
        if (transformed == null)
            return null;

        if (!TryTransferWounds(uid, transformed.Value))
        {
            _polymorph.Revert(transformed.Value);
            return null;
        }

        RemComp<PolymorphedEntityComponent>(transformed.Value);
        QueueDel(uid);
        return transformed;
    }

    private bool TryTransferWounds(EntityUid source, EntityUid target)
    {
        var targetParts = new Dictionary<(BodyPartType, BodyPartSymmetry), EntityUid>();
        foreach (var part in _body.GetBodyChildren(target))
            targetParts.TryAdd((part.Component.PartType, part.Component.Symmetry), part.Id);
        var transfers = new List<(Entity<WoundComponent> Wound, EntityUid SourcePart, EntityUid TargetPart)>();

        foreach (var (sourcePart, part) in _body.GetBodyChildren(source))
        {
            var wounds = _wounds.GetWounds(sourcePart).ToList();
            if (wounds.Count == 0)
                continue;

            if (!targetParts.TryGetValue((part.PartType, part.Symmetry), out var targetPart) ||
                !TryComp<WoundableComponent>(targetPart, out _))
                return false;

            foreach (var wound in wounds)
                transfers.Add((wound, sourcePart, targetPart));
        }

        var transferred = new List<(Entity<WoundComponent> Wound, EntityUid SourcePart)>();
        void Rollback()
        {
            foreach (var (movedWound, originalPart) in transferred)
            {
                _containers.Remove(movedWound.Owner, Comp<WoundableComponent>(movedWound.Comp.HoldingPart).WoundsContainer);
                _containers.Insert(movedWound.Owner, Comp<WoundableComponent>(originalPart).WoundsContainer);
                movedWound.Comp.HoldingPart = originalPart;
                Dirty(movedWound);
            }
        }

        foreach (var (wound, sourcePart, targetPart) in transfers)
        {
            var sourceWoundable = Comp<WoundableComponent>(sourcePart);
            var targetWoundable = Comp<WoundableComponent>(targetPart);
            if (!_containers.Remove(wound.Owner, sourceWoundable.WoundsContainer))
            {
                Rollback();
                return false;
            }

            if (!_containers.Insert(wound.Owner, targetWoundable.WoundsContainer))
            {
                _containers.Insert(wound.Owner, sourceWoundable.WoundsContainer);
                Rollback();
                return false;
            }

            wound.Comp.HoldingPart = targetPart;
            Dirty(wound);
            transferred.Add((wound, sourcePart));
        }

        return true;
    }
}
