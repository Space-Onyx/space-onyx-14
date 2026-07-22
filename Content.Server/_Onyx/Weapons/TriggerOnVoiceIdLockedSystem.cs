using Content.Server.Explosion.EntitySystems;
using Content.Server.Speech;
using Content.Shared.Access.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Weapons;

public sealed partial class TriggerOnVoiceIdLockedSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerOnVoiceIdLockedComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<TriggerOnVoiceIdLockedComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<TriggerOnVoiceIdLockedComponent, ListenEvent>(OnListen);
    }

    private void OnEquipped(Entity<TriggerOnVoiceIdLockedComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.User = args.User;
    }

    private void OnUnequipped(Entity<TriggerOnVoiceIdLockedComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.User = null;
    }

    private void OnListen(Entity<TriggerOnVoiceIdLockedComponent> ent, ref ListenEvent args)
    {
        if (ent.Comp.NextActivationTime > _timing.CurTime ||
            ent.Comp.HolderOnly && args.Source != ent.Comp.User ||
            !args.Message.Trim().Contains(Loc.GetString(ent.Comp.KeyPhrase), StringComparison.InvariantCultureIgnoreCase) ||
            _access.IsAllowed(args.Source, ent) ||
            !TryComp(ent, out ExplosiveComponent? explosive))
            return;

        if (TryComp(args.Source, out HandsComponent? hands))
            _hands.TryDrop((args.Source, hands), ent.Owner, checkActionBlocker: false);

        _explosion.QueueExplosion(ent,
            explosive.ExplosionType,
            explosive.TotalIntensity,
            explosive.IntensitySlope,
            explosive.MaxIntensity,
            explosive.TileBreakScale,
            explosive.MaxTileBreak,
            explosive.CanCreateVacuum,
            args.Source);
        ent.Comp.NextActivationTime = _timing.CurTime + ent.Comp.ActivationCooldown;
    }
}
