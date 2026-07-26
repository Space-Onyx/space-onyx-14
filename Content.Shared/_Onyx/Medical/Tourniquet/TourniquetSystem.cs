using System.Linq;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Medical.Tourniquet;

public sealed partial class TourniquetSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TargetResolverSystem _targeting = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundDamageRoutingSystem _damage = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TourniquetComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<TourniquetComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TourniquetComponent, TourniquetDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(Entity<TourniquetComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStart(ent, args.User, args.User);
    }

    private void OnAfterInteract(Entity<TourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryStart(ent, target, args.User);
    }

    private bool TryStart(Entity<TourniquetComponent> tourniquet, EntityUid body, EntityUid user)
    {
        if (!TryComp(user, out TargetingComponent? targeting) ||
            !_targeting.TryResolveExact(body, targeting.Target, out var part))
        {
            _popup.PopupClient(Loc.GetString("tourniquet-selected-part-missing"), body, user);
            return false;
        }

        if (!CanApply(body, part))
        {
            _popup.PopupClient(Loc.GetString("tourniquet-no-bleeding"), body, user);
            return false;
        }

        _audio.PlayPredicted(tourniquet.Comp.BeginSound, body, user);
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            tourniquet.Comp.Delay,
            new TourniquetDoAfterEvent(GetNetEntity(part)),
            tourniquet,
            body,
            tourniquet)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<TourniquetComponent> tourniquet, ref TourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } body)
            return;

        args.Handled = true;
        var part = GetEntity(args.Part);
        if (!Apply(body, part))
            return;

        if (!tourniquet.Comp.Damage.Empty)
            _damage.TryApplyPartDamage(body, part, tourniquet.Comp.Damage, args.Args.User);
        _audio.PlayPredicted(tourniquet.Comp.EndSound, body, args.Args.User);
        _popup.PopupClient(Loc.GetString("tourniquet-applied"), body, args.Args.User);
        if (_net.IsServer)
            QueueDel(tourniquet);
    }

    public bool Apply(EntityUid body, EntityUid part)
    {
        if (!_net.IsServer || !CanApply(body, part))
            return false;

        var applied = false;
        foreach (var wound in _wounds.GetWounds((part, Comp<WoundableComponent>(part))).ToArray())
        {
            if (!TryComp(wound, out WoundBleedingComponent? bleeding) || bleeding.CurrentRate <= 0f)
                continue;

            applied |= _bleeding.SetTreatment(wound.Owner, BleedingTreatment.Clamped);
        }

        return applied;
    }

    private bool CanApply(EntityUid body, EntityUid part) =>
        _body.BodyHasChild(body, part) && HasComp<WoundableComponent>(part) && _bleeding.GetPartRate(part) > 0f;
}
