using System.Linq;
using Content.Shared.EntityTable;
using Content.Shared.Trigger;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Trigger;

public sealed partial class TriggerSystem : EntitySystem
{
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpawnTableOnTriggerComponent, TriggerEvent>(OnSpawnerTrigger);
        SubscribeLocalEvent<TriggerCounterComponent, TriggerEvent>(OnTriggerCounter);
        SubscribeLocalEvent<TriggerCounterLimitComponent, AttemptTriggerEvent>(OnTriggerLimitCounter);
    }

    private void OnSpawnerTrigger(Entity<SpawnTableOnTriggerComponent> ent, ref TriggerEvent args)
    {
        var target = ent.Comp.TargetUser ? args.User : ent.Owner;
        if (target == null)
            return;

        var xform = Transform(target.Value);
        var spawns = _entityTable.GetSpawns(ent.Comp.Table, _random).ToList();
        if (ent.Comp.UseMapCoords)
        {
            var coordinates = _transform.GetMapCoordinates(target.Value, xform);
            foreach (var spawn in spawns)
            {
                if (ent.Comp.Predicted)
                    EntityManager.PredictedSpawn(spawn, coordinates);
                else if (_net.IsServer)
                    Spawn(spawn, coordinates);
            }
            return;
        }

        if (!xform.Coordinates.IsValid(EntityManager))
            return;

        foreach (var spawn in spawns)
        {
            if (ent.Comp.Predicted)
                PredictedSpawnAttachedTo(spawn, xform.Coordinates);
            else if (_net.IsServer)
                SpawnAttachedTo(spawn, xform.Coordinates);
        }
    }

    private void OnTriggerCounter(Entity<TriggerCounterComponent> ent, ref TriggerEvent args)
    {
        if (!args.Handled)
            ent.Comp.Count++;
    }

    private void OnTriggerLimitCounter(Entity<TriggerCounterLimitComponent> ent, ref AttemptTriggerEvent args)
    {
        if (TryComp(ent.Owner, out TriggerCounterComponent? counter) && counter.Count >= ent.Comp.MaxCount)
            args.Cancelled = true;
    }
}
