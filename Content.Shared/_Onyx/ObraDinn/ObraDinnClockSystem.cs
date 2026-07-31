using Content.Shared._Onyx.Holograms;
using Content.Shared.Body;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.ObraDinn;

public sealed partial class ObraDinnClockSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObraDinnClockComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ObraDinnClockComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnUseInHand(Entity<ObraDinnClockComponent> ent, ref UseInHandEvent args)
    {
        if (_timing.CurTime < ent.Comp.Cooldown)
            return;

        ent.Comp.Cooldown = _timing.CurTime + ent.Comp.CooldownTime;
        if (ent.Comp.Map == null && ent.Comp.Location == null)
        {
            _popup.PopupEntity(Loc.GetString("obradinn-activate-fail-case"), args.User, args.User);
            return;
        }

        if (ent.Comp.Map == null || ent.Comp.Map != Transform(args.User).MapID)
        {
            _popup.PopupEntity(Loc.GetString("obradinn-activate-fail-map"), args.User, args.User);
            return;
        }

        if (ent.Comp.Location == null ||
            !Transform(args.User).Coordinates.TryDistance(EntityManager, ent.Comp.Location.Value, out var distance))
        {
            _popup.PopupEntity(Loc.GetString("obradinn-activate-fail-no-distance"), args.User, args.User);
            return;
        }

        if (distance > ent.Comp.DistanceFromCrimeScene)
        {
            _popup.PopupEntity(
                Loc.GetString("obradinn-activate-fail-distance", ("distance", Math.Round(distance))),
                args.User,
                args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("obradinn-activate-success"), args.User, args.User);
        Activate(ent);

        ent.Comp.Location = null;
        ent.Comp.Map = null;
        ent.Comp.Witnesses.Clear();
        ent.Comp.Cooldown = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Lifetime);
        Dirty(ent);
    }

    private void OnInteract(Entity<ObraDinnClockComponent> ent, ref AfterInteractEvent args)
    {
        if (_timing.CurTime < ent.Comp.Cooldown || args.Target is not { } target)
            return;

        ent.Comp.Cooldown = _timing.CurTime + ent.Comp.CooldownTime;
        if (!_mobState.IsDead(target) || !TryComp(target, out ObraDinnBodyComponent? body))
        {
            _popup.PopupEntity(Loc.GetString("obradinn-interact-fail-target"), args.User, args.User);
            return;
        }

        ent.Comp.Location = body.Location;
        ent.Comp.Map = body.Map;
        ent.Comp.Witnesses.Clear();
        ent.Comp.Witnesses.AddRange(body.Witnesses);
        _popup.PopupEntity(Loc.GetString("obradinn-interact-success"), args.User, args.User);
        Dirty(ent);
    }

    private void Activate(Entity<ObraDinnClockComponent> ent)
    {
        foreach (var witness in ent.Comp.Witnesses)
        {
            if (TerminatingOrDeleted(witness.Uid) || MetaData(witness.Uid).EntityPrototype is not { } prototype)
                continue;

            var hologram = PredictedSpawnAtPosition(prototype.ID, witness.Location);
            _visualBody.CopyAppearanceFrom(witness.Uid, hologram);
            _metaData.SetEntityName(hologram, Loc.GetString("obradinn-hologram-name"));
            _mobState.ChangeMobState(hologram, witness.MobState);

            EnsureComp<TimedDespawnComponent>(hologram).Lifetime = ent.Comp.Lifetime;
            EnsureComp<HologramVisualsComponent>(hologram);
            EnsureComp<ObraDinnHologramComponent>(hologram).RealName = witness.Name;
        }
    }
}
