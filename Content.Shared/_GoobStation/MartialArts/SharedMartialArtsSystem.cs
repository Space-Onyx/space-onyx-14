using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.MartialArts;

public sealed partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComboAttackPerformedEvent>(OnAttack);
        SubscribeLocalEvent<GrantMartialArtKnowledgeComponent, UseInHandEvent>(OnUseManual);
        SubscribeLocalEvent<KravMagaSilencedComponent, SpeakAttemptEvent>(OnSpeakAttempt);
    }

    public override void Update(float frameTime)
    {
        var silence = EntityQueryEnumerator<KravMagaSilencedComponent>();
        while (silence.MoveNext(out var uid, out var comp))
            if (_timing.CurTime >= comp.Until)
                RemCompDeferred<KravMagaSilencedComponent>(uid);

        var breathing = EntityQueryEnumerator<KravMagaBlockedBreathingComponent>();
        while (breathing.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.Until)
                RemCompDeferred<KravMagaBlockedBreathingComponent>(uid);
            else if (_net.IsServer)
                _stamina.TakeStaminaDamage(uid, frameTime * 3f, source: uid);
        }
    }

    public bool TrySetForm(EntityUid uid, MartialArtsForms? form)
    {
        if (!Exists(uid))
            return false;

        if (form is null)
        {
            RemComp<MartialArtsKnowledgeComponent>(uid);
            RemComp<CanPerformComboComponent>(uid);
            return true;
        }

        var knowledge = EnsureComp<MartialArtsKnowledgeComponent>(uid);
        knowledge.MartialArtsForm = form.Value;
        EnsureComp<CanPerformComboComponent>(uid).LastAttacks.Clear();
        Dirty(uid, knowledge);
        return true;
    }

    private void OnUseManual(Entity<GrantMartialArtKnowledgeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        TrySetForm(args.User, ent.Comp.MartialArtsForm);
        _popup.PopupEntity(Loc.GetString("martial-arts-learned", ("form", ent.Comp.MartialArtsForm)), args.User, args.User);
        args.Handled = true;
        if (!ent.Comp.MultiUse)
            QueueDel(ent);
    }

    private void OnAttack(Entity<MartialArtsKnowledgeComponent> ent, ref ComboAttackPerformedEvent args)
    {
        if (!TryComp<CanPerformComboComponent>(ent, out var combo)
            || !HasComp<MobStateComponent>(args.Target)
            || args.Weapon != ent.Owner)
            return;

        if (combo.CurrentTarget != args.Target || _timing.CurTime > combo.ResetAt)
            combo.LastAttacks.Clear();

        combo.CurrentTarget = args.Target;
        combo.ResetAt = _timing.CurTime + combo.ResetAfter;
        combo.LastAttacks.Add(args.Type);
        if (combo.LastAttacks.Count > 4)
            combo.LastAttacks.RemoveAt(0);

        if (!_prototypes.TryIndex<MartialArtPrototype>(ent.Comp.MartialArtsForm.ToString(), out var art)
            || !_prototypes.TryIndex(art.RoundstartCombos, out var list))
            return;

        foreach (var id in list.Combos)
        {
            var move = _prototypes.Index(id);
            if (move.AttackTypes.Count > combo.LastAttacks.Count
                || !combo.LastAttacks.TakeLast(move.AttackTypes.Count).SequenceEqual(move.AttackTypes)
                || !move.CanDoWhileProne && _standing.IsDown(ent.Owner))
                continue;

            combo.LastAttacks.Clear();
            if (_net.IsServer)
                PerformMove(ent, move.PerformOnSelf ? ent.Owner : args.Target, move);
            break;
        }
    }

    private void PerformMove(EntityUid performer, EntityUid target, ComboPrototype move)
    {
        if (move.ExtraDamage > 0 && _prototypes.TryIndex<DamageTypePrototype>(move.DamageType, out var type))
            _damage.TryChangeDamage(target, new DamageSpecifier(type, move.ExtraDamage), origin: performer);
        if (move.StaminaDamage != 0)
            _stamina.TakeStaminaDamage(target, move.StaminaDamage, source: performer);
        if (move.ParalyzeTime > 0)
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(move.ParalyzeTime), drop: move.DropItems, force: true);
        if (move.ThrowTarget)
        {
            var from = _transform.GetMapCoordinates(performer);
            var to = _transform.GetMapCoordinates(target);
            if (from.MapId == to.MapId)
                _throwing.TryThrow(target, to.Position - from.Position, move.ThrownSpeed, performer);
        }
        if (move.SilenceTime > 0)
            EnsureComp<KravMagaSilencedComponent>(target).Until = _timing.CurTime + TimeSpan.FromSeconds(move.SilenceTime);
        if (move.BlockBreathingTime > 0)
            EnsureComp<KravMagaBlockedBreathingComponent>(target).Until = _timing.CurTime + TimeSpan.FromSeconds(move.BlockBreathingTime);

        _popup.PopupEntity(Loc.GetString("martial-arts-move", ("move", move.Name)), target, performer, PopupType.Medium);
    }

    private void OnSpeakAttempt(Entity<KravMagaSilencedComponent> ent, ref SpeakAttemptEvent args)
    {
        if (_timing.CurTime < ent.Comp.Until)
            args.Cancel();
    }
}
