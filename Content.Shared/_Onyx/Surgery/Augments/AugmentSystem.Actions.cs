using Content.Shared.Actions.Components;
using Content.Shared.Body.Part;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem
{
    private void InitializeActions()
    {
        SubscribeLocalEvent<AugmentActionComponent, AugmentToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<AugmentActionComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<AugmentActivatableUIComponent, AugmentActionEvent>(OnOpenUi);
    }

    private void GrantAction(EntityUid augment, EntityUid body)
    {
        if (!TryComp(augment, out AugmentActionComponent? action))
            return;
        if (HasComp<AugmentItemPanelComponent>(augment) && TryComp(Transform(augment).ParentUid, out BodyPartComponent? part))
        {
            action.Action = part.Symmetry switch
            {
                BodyPartSymmetry.Left => "ActionAugmentToggleItemPanelLeft",
                BodyPartSymmetry.Right => "ActionAugmentToggleItemPanelRight",
                _ => "ActionAugmentToggleItemPanel",
            };
        }
        EnsureComp<ActionsContainerComponent>(augment);
        if (_actionContainer.EnsureAction(augment, ref action.ActionEntity, action.Action))
        {
            _actions.GrantContainedAction(body, augment, action.ActionEntity.Value);
            if (TryComp(augment, out AugmentItemPanelComponent? panel))
            {
                _actions.SetIcon(action.ActionEntity.Value, panel.Icon);
                _actions.SetUseDelay(action.ActionEntity.Value,
                    panel.ActionCooldown > TimeSpan.Zero ? panel.ActionCooldown : null);
            }
        }
        Dirty(augment, action);
    }

    private void RevokeAction(EntityUid augment, EntityUid body)
    {
        if (TryComp(augment, out AugmentActionComponent? action) && action.ActionEntity is { } actionEntity)
            _actions.RemoveProvidedAction(body, augment, actionEntity);
    }

    private void OnToggleAction(Entity<AugmentActionComponent> ent, ref AugmentToggleActionEvent args)
    {
        if (!CanUse(ent.Owner, args.Performer))
            return;
        _toggle.Toggle(ent.Owner, args.Performer);
        args.Handled = true;
    }

    private void OnToggled(Entity<AugmentActionComponent> ent, ref ItemToggledEvent args)
    {
        _actions.SetToggled(ent.Comp.ActionEntity, args.Activated);
        if (GetBody(ent.Owner) is { } body)
            RefreshPower(body);
    }

    private void OnOpenUi(Entity<AugmentActivatableUIComponent> ent, ref AugmentActionEvent args)
    {
        if (!CanUse(ent.Owner, args.Performer) || ent.Comp.Key is not { } key || !_ui.HasUi(ent.Owner, key))
            return;
        _ui.OpenUi(ent.Owner, key, args.Performer);
        args.Handled = true;
    }
}
