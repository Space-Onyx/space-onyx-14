using Content.Shared.Damage.Components;
// <Onyx-PartPain>
using Content.Shared._Onyx.Wounds;
// </Onyx-PartPain>
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;

namespace Content.Client.UserInterface.Systems.DamageOverlays;

[UsedImplicitly]
public sealed partial class DamageOverlayUiController : UIController
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    [UISystemDependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [UISystemDependency] private readonly DamageableSystem _damageable = default!;
    // <Onyx-PartPain>
    [UISystemDependency] private readonly PainSystem _pain = default!;
    // </Onyx-PartPain>
    private Overlays.DamageOverlay _overlay = default!;

    public override void Initialize()
    {
        _overlay = new Overlays.DamageOverlay();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdChecked>(OnThresholdCheck);
        // <Onyx-PartPain-edited>
        EntityManager.EventBus.SubscribeLocalEvent<PainComponent, AfterAutoHandleStateEvent>(OnPainState);
        // </Onyx-PartPain-edited>
    }

    // <Onyx-PartPain-edited>
    private void OnPainState(EntityUid uid, PainComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (uid == _playerManager.LocalEntity)
            UpdateOverlays(uid, EntityManager.GetComponentOrNull<MobStateComponent>(uid));
    }
    // </Onyx-PartPain-edited>

    private void OnPlayerAttach(LocalPlayerAttachedEvent args)
    {
        ClearOverlay();
        if (!EntityManager.TryGetComponent<MobStateComponent>(args.Entity, out var mobState))
            return;
        if (mobState.CurrentState != MobState.Dead)
            UpdateOverlays(args.Entity, mobState);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.Target != _playerManager.LocalEntity)
            return;

        UpdateOverlays(args.Target, args.Component);
    }

    private void OnThresholdCheck(ref MobThresholdChecked args)
    {

        if (args.Target != _playerManager.LocalEntity)
            return;
        UpdateOverlays(args.Target, args.MobState, args.Damageable, args.Threshold);
    }

    private void ClearOverlay()
    {
        _overlay.State = MobState.Alive;
        _overlay.DeadLevel = 0f;
        _overlay.CritLevel = 0f;
        _overlay.PainLevel = 0f;
        _overlay.OxygenLevel = 0f;
    }

    //TODO: Jezi: adjust oxygen and hp overlays to use appropriate systems once bodysim is implemented
    private void UpdateOverlays(EntityUid entity, MobStateComponent? mobState, DamageableComponent? damageable = null, MobThresholdsComponent? thresholds = null, InjurableComponent? injurable = null)
    {
        if (mobState == null && !EntityManager.TryGetComponent(entity, out mobState) ||
            thresholds == null && !EntityManager.TryGetComponent(entity, out thresholds) ||
            damageable == null && !EntityManager.TryGetComponent(entity, out  damageable) ||
            injurable == null && !EntityManager.TryGetComponent(entity, out injurable))
            return;

        if (!_mobThresholdSystem.TryGetIncapThreshold(entity, out var foundThreshold, thresholds))
            return; //this entity cannot die or crit!!

        if (!thresholds.ShowOverlays)
        {
            ClearOverlay();
            return; //this entity intentionally has no overlays
        }

        var damagePerGroup = _damageable.GetDamagePerGroup((entity, damageable));
        var critThreshold = foundThreshold.Value;
        _overlay.State = mobState.CurrentState;

        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            {
                FixedPoint2 painLevel = 0;
                _overlay.PainLevel = 0;

                // <Onyx-PartPain-edited>
                if (EntityManager.TryGetComponent(entity, out PainComponent? pain))
                {
                    _overlay.PainLevel = FixedPoint2.Min(1f, _pain.GetPain((entity, pain)) / critThreshold).Float();
                }
                else
                {
                    foreach (var painDamageType in injurable.PainDamageGroups)
                    {
                        damagePerGroup.TryGetValue(painDamageType, out var painDamage);
                        painLevel += painDamage;
                    }
                    _overlay.PainLevel = FixedPoint2.Min(1f, painLevel / critThreshold).Float();
                }
                // </Onyx-PartPain-edited>

                if (_overlay.PainLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                {
                    _overlay.PainLevel = 0;
                }

                if (damagePerGroup.TryGetValue("Airloss", out var oxyDamage))
                {
                    _overlay.OxygenLevel = FixedPoint2.Min(1f, oxyDamage / critThreshold).Float();
                }

                _overlay.CritLevel = 0;
                _overlay.DeadLevel = 0;
                break;
            }
            case MobState.Critical:
            {
                if (!_mobThresholdSystem.TryGetDeadPercentage(entity,
                        FixedPoint2.Max(0.0, _damageable.GetTotalDamage((entity, damageable))), out var critLevel))
                    return;
                _overlay.CritLevel = critLevel.Value.Float();

                _overlay.PainLevel = 0;
                _overlay.DeadLevel = 0;
                break;
            }
            case MobState.Dead:
            {
                _overlay.PainLevel = 0;
                _overlay.CritLevel = 0;
                break;
            }
        }
    }
}
