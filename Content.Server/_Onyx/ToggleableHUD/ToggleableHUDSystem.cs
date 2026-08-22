using Content.Server.Actions;
using Content.Shared._Onyx.ToggleableHUD;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.ToggleableHUD;

/// <summary>
///     System for toggling HUDs.
/// </summary>
public sealed partial class ToggleableHudSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableHUDComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleableHUDComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ToggleableHUDComponent, ToggleActionEvent>(OnToggle);
    }

    private void OnMapInit(EntityUid uid, ToggleableHUDComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
    }

    private void OnShutdown(EntityUid uid, ToggleableHUDComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnToggle(EntityUid uid, ToggleableHUDComponent component, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggle(uid, component);
    }

    public bool TryToggle(EntityUid uid, ToggleableHUDComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var actionToggle = !component.IsToggled;

        var popupKey = actionToggle ? component.PopupToggleOn : component.PopupToggleOff;
        var popupMessage = Loc.GetString(popupKey);

        // 这是个好办法，我发誓 😱
        if (actionToggle)
        {
            EnsureComp<ShowJobIconsComponent>(uid);
            EnsureComp<ShowMindShieldIconsComponent>(uid);
            EnsureComp<ShowCriminalRecordIconsComponent>(uid);

            // HealthBars should have a list of damage containers
            var healthBars = EnsureComp<ShowHealthBarsComponent>(uid);
            healthBars.DamageContainers = new List<ProtoId<DamageContainerPrototype>>(component.DamageContainers);
            Dirty(uid, healthBars);
            EnsureComp<ShowHealthIconsComponent>(uid);

            EnsureComp<ShowSyndicateIconsComponent>(uid);

            var satiation = EnsureComp<ShowSatiationIconsComponent>(uid);
            satiation.ShownTypes = [SatiationSystem.Hunger, SatiationSystem.Thirst];
            Dirty(uid, satiation);
        }
        else
        {
            RemComp<ShowJobIconsComponent>(uid);
            RemComp<ShowMindShieldIconsComponent>(uid);
            RemComp<ShowCriminalRecordIconsComponent>(uid);

            RemComp<ShowHealthBarsComponent>(uid);
            RemComp<ShowHealthIconsComponent>(uid);

            RemComp<ShowSyndicateIconsComponent>(uid);

            RemComp<ShowSatiationIconsComponent>(uid);
        }

        _popup.PopupEntity(popupMessage, uid, uid);

        component.IsToggled = actionToggle;
        Dirty(uid, component);

        return true;
    }
}
