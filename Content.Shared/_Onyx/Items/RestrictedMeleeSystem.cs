using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.Items;

public sealed partial class RestrictedMeleeSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RestrictedMeleeComponent, AttemptMeleeEvent>(OnMeleeAttempt);
    }

    private void OnMeleeAttempt(Entity<RestrictedMeleeComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.Whitelist != null && _whitelist.IsValid(ent.Comp.Whitelist, args.User))
            return;

        args.Message = Loc.GetString(ent.Comp.FailText, ("item", ent.Owner));
        if (ent.Comp.DoKnockdown && _stun.TryKnockdown(args.User, ent.Comp.KnockdownDuration, force: true))
            _audio.PlayPredicted(ent.Comp.FallSound, args.User, args.User);
        if (ent.Comp.ForceDrop)
            _hands.TryDrop(args.User);

        _popup.PopupClient(args.Message, ent.Owner, args.User, PopupType.Large);
        args.Cancelled = true;
    }
}
