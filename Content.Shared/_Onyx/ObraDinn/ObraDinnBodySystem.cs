using Content.Shared.Body;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Onyx.ObraDinn;

public sealed partial class ObraDinnBodySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VisualBodyComponent, ComponentStartup>(OnHumanoidStartup);
        SubscribeLocalEvent<ObraDinnBodyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ObraDinnBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnHumanoidStartup(Entity<VisualBodyComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<ObraDinnBodyComponent>(ent);
    }

    private void OnStartup(Entity<ObraDinnBodyComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Location = Transform(ent).Coordinates;
    }

    private void OnMobStateChanged(Entity<ObraDinnBodyComponent> ent, ref MobStateChangedEvent args)
    {
        ent.Comp.Witnesses.Clear();
        ent.Comp.Map = null;

        if (args.NewMobState != MobState.Dead || TerminatingOrDeleted(ent))
        {
            Dirty(ent);
            return;
        }

        ent.Comp.Location = Transform(ent).Coordinates;
        ent.Comp.Map = Transform(ent).MapID;

        foreach (var witness in _lookup.GetEntitiesInRange(ent.Comp.Location.Value, ent.Comp.WitnessRange))
        {
            if (!TryComp(witness, out MobStateComponent? mobState))
                continue;

            ent.Comp.Witnesses.Add(new ObraDinnWitness(
                witness,
                Transform(witness).Coordinates,
                Identity.Name(witness, EntityManager, ent),
                mobState.CurrentState));
        }

        Dirty(ent);
    }
}
