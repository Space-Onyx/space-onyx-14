using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared._Onyx.Storage;

public abstract partial class SharedMouthStorageSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MouthStorageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MouthStorageComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<MouthStorageComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<MouthStorageComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<MouthStorageComponent, ExaminedEvent>(OnExamined);
    }

    protected bool TryGetOccupiedMouth(MouthStorageComponent component, out StorageComponent storage)
    {
        if (component.MouthId is { } mouth && TryComp<StorageComponent>(mouth, out var found))
        {
            storage = found;
            return storage.Container.ContainedEntities.Count > 0;
        }

        storage = default!;
        return false;
    }

    private void OnMapInit(Entity<MouthStorageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Mouth = _containers.EnsureContainer<Container>(ent, MouthStorageComponent.MouthContainerId);
        ent.Comp.Mouth.ShowContents = false;
        ent.Comp.Mouth.OccludesLight = false;

        var mouth = Spawn(ent.Comp.MouthProto, new EntityCoordinates(ent, 0, 0));
        if (!_containers.Insert(mouth, ent.Comp.Mouth))
        {
            QueueDel(mouth);
            return;
        }

        ent.Comp.MouthId = mouth;
        if (ent.Comp.OpenStorageAction is { } action)
            _actions.AddAction(ent, ref ent.Comp.Action, action, mouth);
        Dirty(ent);
    }

    private void OnDowned(Entity<MouthStorageComponent> ent, ref DownedEvent args)
        => Dump(ent);

    private void OnDisarmed(Entity<MouthStorageComponent> ent, ref DisarmedEvent args)
        => Dump(ent);

    private void OnDamageDealt(Entity<MouthStorageComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() >= ent.Comp.SpitDamageThreshold)
            Dump(ent);
    }

    private void Dump(Entity<MouthStorageComponent> ent)
    {
        if (!TryGetOccupiedMouth(ent.Comp, out var storage))
            return;

        _containers.EmptyContainer(storage.Container, force: true, destination: Transform(ent).Coordinates);
    }

    private void OnExamined(Entity<MouthStorageComponent> ent, ref ExaminedEvent args)
    {
        if (TryGetOccupiedMouth(ent.Comp, out _))
            args.PushMarkup(Loc.GetString("mouth-storage-examine-condition-occupied",
                ("entity", Identity.Entity(ent, EntityManager))));
    }
}
