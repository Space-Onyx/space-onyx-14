using Content.Shared._Onyx.CloneProjector;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;

namespace Content.Server._Onyx.CloneProjector;

public sealed partial class CloneProjectorSystem
{
    private void InitializeClone()
    {
        SubscribeLocalEvent<HolographicCloneComponent, MobStateChangedEvent>(OnCloneStateChanged);
        SubscribeLocalEvent<HolographicCloneComponent, ExaminedEvent>(OnCloneExamined);
        SubscribeLocalEvent<HolographicCloneComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<HolographicCloneComponent, DamageModifyEvent>(OnCloneDamageModify);
    }

    private void OnCloneDamageModify(Entity<HolographicCloneComponent> clone, ref DamageModifyEvent args)
    {
        if (clone.Comp.HostProjector is not { } projector || IsCloneDeployed(projector.Comp))
            return;

        args.Damage *= 0;
    }

    private void OnCloneStateChanged(Entity<HolographicCloneComponent> clone, ref MobStateChangedEvent args)
    {
        if (!_mobState.IsIncapacitated(clone) || clone.Comp.HostProjector is not { } projector)
            return;

        TryInsertClone(projector, true);
        RaiseLocalEvent(clone, new RejuvenateEvent());

        if (clone.Comp.HostEntity is not { } host)
            return;

        _popup.PopupEntity(Loc.GetString("gemini-projector-clone-destroyed"), host, host, PopupType.LargeCaution);

        if (!projector.Comp.DoStun || !HasComp<WearingCloneProjectorComponent>(host))
            return;

        _stun.TryUpdateParalyzeDuration(host, projector.Comp.StunDuration);
        _damageable.TryChangeDamage(host, projector.Comp.DamageOnDestroyed, true);
    }

    private void OnCloneExamined(Entity<HolographicCloneComponent> clone, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || clone.Comp.HostProjector is not { } projector)
            return;

        args.PushMarkup(Loc.GetString(projector.Comp.FlavorText));
    }

    private void OnEmpPulse(Entity<HolographicCloneComponent> clone, ref EmpPulseEvent args)
    {
        if (clone.Comp.HostProjector is not { } projector || clone.Comp.HostEntity is not { } host)
            return;

        args.Disabled = true;
        args.Affected = true;

        var duration = args.Duration > projector.Comp.StunDuration ? projector.Comp.StunDuration : args.Duration;
        TryInsertClone(projector, true);

        if (projector.Comp.DoStun)
            _stun.TryUpdateParalyzeDuration(host, duration);

        _popup.PopupEntity(Loc.GetString("gemini-projector-clone-destroyed"), host, host, PopupType.LargeCaution);
    }
}
