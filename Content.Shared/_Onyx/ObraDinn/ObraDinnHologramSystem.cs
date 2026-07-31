using Content.Shared._Onyx.Carrying;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Storage.Components;
using Content.Shared.Strip.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Shared._Onyx.ObraDinn;

public sealed partial class ObraDinnHologramSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObraDinnHologramComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<ObraDinnHologramComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ObraDinnHologramComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ObraDinnHologramComponent, InsertIntoEntityStorageAttemptEvent>(OnStorageAttempt);
    }

    private void OnListen(Entity<ObraDinnHologramComponent> ent, ref ListenEvent args)
    {
        if (!args.Message.Equals(ent.Comp.RealName, StringComparison.OrdinalIgnoreCase) ||
            !Transform(ent).Coordinates.TryDistance(EntityManager, Transform(args.Source).Coordinates, out var distance) ||
            distance > ent.Comp.ListenRange)
            return;

        _metaData.SetEntityName(ent, ent.Comp.RealName);
        SpawnAtPosition(ent.Comp.SpawnEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    private void OnStartup(Entity<ObraDinnHologramComponent> ent, ref ComponentStartup args)
    {
        SpawnAtPosition(ent.Comp.SpawnEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        EnsureComp<ActiveListenerComponent>(ent).Range = ent.Comp.ListenRange;

        RemCompDeferred<PullableComponent>(ent);
        RemCompDeferred<WoundableComponent>(ent);
        RemCompDeferred<ActorComponent>(ent);
        RemCompDeferred<MindContainerComponent>(ent);
        RemCompDeferred<FixturesComponent>(ent);
        RemCompDeferred<PhysicsComponent>(ent);
        RemCompDeferred<StrippableComponent>(ent);
        RemCompDeferred<SSDIndicatorComponent>(ent);
        RemCompDeferred<GhostRoleMobSpawnerComponent>(ent);
        RemCompDeferred<DamageableComponent>(ent);
        RemCompDeferred<MobMoverComponent>(ent);
        RemCompDeferred<CarriableComponent>(ent);
        RemCompDeferred<MobStateComponent>(ent);
    }

    private void OnShutdown(Entity<ObraDinnHologramComponent> ent, ref ComponentShutdown args)
    {
        var effect = SpawnAtPosition(ent.Comp.SpawnEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.Sound, effect);
    }

    private void OnStorageAttempt(Entity<ObraDinnHologramComponent> ent, ref InsertIntoEntityStorageAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
