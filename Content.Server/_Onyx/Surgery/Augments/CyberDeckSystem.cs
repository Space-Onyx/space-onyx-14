using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.PDA;
using Content.Shared.UserInterface;
using Content.Shared._Onyx.Surgery.Augments;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class CyberDeckSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AugmentModuleSystem _modules = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentModuleHostComponent, AugmentModulesChangedEvent>(OnModulesChanged);
        SubscribeLocalEvent<CyberDeckComponent, AugmentModuleDetachedEvent>(OnDeckDetached);
        SubscribeLocalEvent<CyberDeckComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyberDeckComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyberDeckComponent, CyberDeckOpenActionEvent>(OnOpen);
    }

    private void OnModulesChanged(Entity<AugmentModuleHostComponent> ent, ref AugmentModulesChangedEvent args)
    {
        if (TryComp(ent, out CyberDeckComponent? deck))
            Reconcile((ent.Owner, deck));
        foreach (var module in _modules.GetModules(ent))
            if (TryComp(module, out deck))
                Reconcile((module, deck));
    }

    private void OnDeckDetached(Entity<CyberDeckComponent> ent, ref AugmentModuleDetachedEvent args)
    {
        if (TryComp(ent, out AugmentModuleServicePanelComponent? panel))
        {
            panel.Open = false;
            Dirty(ent.Owner, panel);
        }
        Reconcile(ent);
    }

    private void OnStartup(Entity<CyberDeckComponent> ent, ref ComponentStartup args) => Reconcile(ent);

    private void OnShutdown(Entity<CyberDeckComponent> ent, ref ComponentShutdown args)
    {
        RevokeActions(ent);
    }

    private void OnOpen(Entity<CyberDeckComponent> ent, ref CyberDeckOpenActionEvent args)
    {
        if (args.Handled || !CanUse(ent, args.Performer) || !_ui.HasUi(ent.Owner, PdaUiKey.Key))
            return;
        if (!_ui.TryOpenUi(ent.Owner, PdaUiKey.Key, args.Performer))
            return;
        args.Handled = true;
    }

    private void Reconcile(Entity<CyberDeckComponent> ent)
    {
        var body = _modules.GetInstalledBody(ent);
        if (body != ent.Comp.GrantedBody)
        {
            RevokeActions(ent);
            ent.Comp.GrantedBody = body;
        }
        if (TryComp(ent, out PdaComponent? pda))
            pda.PdaOwner = body;
        if (body is not { } owner)
            return;

        EnsureComp<ActionsContainerComponent>(ent);
        if (_actionContainer.EnsureAction(ent, ref ent.Comp.OpenActionEntity, ent.Comp.OpenAction))
            _actions.GrantContainedAction(owner, ent.Owner, ent.Comp.OpenActionEntity.Value);
        Dirty(ent);
    }

    private void RevokeActions(Entity<CyberDeckComponent> ent)
    {
        if (ent.Comp.GrantedBody is not { } body)
            return;
        if (ent.Comp.OpenActionEntity is { } openAction)
            _actions.RemoveProvidedAction(body, ent.Owner, openAction);
        ent.Comp.GrantedBody = null;
    }

    private bool CanUse(EntityUid deck, EntityUid performer) =>
        _modules.GetInstalledBody(deck) == performer &&
        (!TryComp(deck, out NeuroInterfaceRuntimeComponent? runtime) || runtime.ManuallyEnabled) &&
        !HasComp<Content.Shared.Emp.EmpDisabledComponent>(deck);
}
