using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared._Onyx.Surgery.Augments;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class CyberDeckSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AugmentModuleSystem _modules = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const float MinRegenTime = 0.01f;
    private readonly HashSet<EntityUid> _regeneratingDecks = new();
    private readonly List<EntityUid> _regeneratingDeckBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentModuleHostComponent, AugmentModulesChangedEvent>(OnModulesChanged);
        SubscribeLocalEvent<CyberDeckComponent, AugmentModuleDetachedEvent>(OnDeckDetached);
        SubscribeLocalEvent<CyberDeckScriptComponent, AugmentModuleDetachedEvent>(OnScriptDetached);
        SubscribeLocalEvent<CyberDeckComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyberDeckComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyberDeckComponent, CyberDeckOpenActionEvent>(OnOpen);
        SubscribeLocalEvent<CyberDeckComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CyberDeckScriptComponent, CyberDeckScriptActionEvent>(OnScriptAction);
        SubscribeLocalEvent<CyberDeckScriptComponent, CyberDeckScriptTargetActionEvent>(OnScriptTargetAction);
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

    private void OnScriptDetached(Entity<CyberDeckScriptComponent> ent, ref AugmentModuleDetachedEvent args)
    {
        if (TryComp(args.Host, out CyberDeckComponent? deck) && deck.GrantedBody is { } body && ent.Comp.ActionEntity is { } action)
            _actions.RemoveProvidedAction(body, ent.Owner, action);
    }

    private void OnStartup(Entity<CyberDeckComponent> ent, ref ComponentStartup args) => Reconcile(ent);

    private void OnShutdown(Entity<CyberDeckComponent> ent, ref ComponentShutdown args)
    {
        RevokeActions(ent);
        _regeneratingDecks.Remove(ent);
    }

    private void OnOpen(Entity<CyberDeckComponent> ent, ref CyberDeckOpenActionEvent args)
    {
        if (args.Handled || !CanUse(ent, args.Performer) || !_ui.HasUi(ent.Owner, PdaUiKey.Key))
            return;
        if (!_ui.TryOpenUi(ent.Owner, PdaUiKey.Key, args.Performer))
            return;
        args.Handled = true;
    }

    private void OnExamined(Entity<CyberDeckComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("cyberdeck-examine-ram", ("current", ent.Comp.CurrentRam), ("maximum", ent.Comp.MaxRam)));
    }

    private void OnScriptAction(Entity<CyberDeckScriptComponent> ent, ref CyberDeckScriptActionEvent args)
    {
        if (args.Handled || !TryExecuteScript(ent.AsNullable(), args.Performer, null, null))
            return;
        args.Handled = true;
    }

    private void OnScriptTargetAction(Entity<CyberDeckScriptComponent> ent, ref CyberDeckScriptTargetActionEvent args)
    {
        if (args.Handled || !TryExecuteScript(ent.AsNullable(), args.Performer, args.Entity, args.Target))
            return;
        args.Handled = true;
    }

    public bool TryExecuteScript(
        Entity<CyberDeckScriptComponent?> script,
        EntityUid performer,
        EntityUid? target,
        Robust.Shared.Map.EntityCoordinates? coordinates)
    {
        if (!Resolve(script, ref script.Comp, false) ||
            !TryGetExecution((script.Owner, script.Comp), performer, out var deck, out var body))
            return false;

        var attempt = new CyberDeckScriptExecutionAttemptEvent(body, performer, target, coordinates);
        RaiseLocalEvent(script, ref attempt);
        if (attempt.Cancelled)
            return false;

        var uiOpened = false;
        if (TryComp(script, out CyberDeckScriptActivatableUIComponent? activatable) &&
            activatable.Key is { } key && _ui.HasUi(script.Owner, key))
        {
            if (!HasRam(deck, script.Comp.RamCost))
            {
                PopupNoRam(body);
                return false;
            }

            if (!_ui.TryOpenUi(script.Owner, key, body))
                return false;
            uiOpened = true;
        }

        if (!TrySpendRam(deck, script.Comp.RamCost))
        {
            PopupNoRam(body);
            return false;
        }

        var executed = new CyberDeckScriptExecutedEvent(body, deck, performer, attempt.Target, attempt.Coordinates);
        RaiseLocalEvent(script, ref executed);
        if (uiOpened || executed.Handled)
            return true;

        RestoreRam(deck, script.Comp.RamCost);
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _regeneratingDeckBuffer.Clear();
        _regeneratingDeckBuffer.AddRange(_regeneratingDecks);
        foreach (var uid in _regeneratingDeckBuffer)
        {
            if (!TryComp(uid, out CyberDeckComponent? deck) || deck.CurrentRam >= deck.MaxRam)
            {
                _regeneratingDecks.Remove(uid);
                continue;
            }
            var interval = float.IsFinite(deck.RamRegenTime) ? MathF.Max(MinRegenTime, deck.RamRegenTime) : MinRegenTime;
            deck.RegenAccumulator += frameTime;
            var steps = (int) (deck.RegenAccumulator / interval);
            if (steps <= 0)
                continue;
            deck.RegenAccumulator -= steps * interval;
            deck.CurrentRam = MathF.Min(deck.MaxRam, deck.CurrentRam + steps);
            Dirty(uid, deck);
            if (deck.CurrentRam >= deck.MaxRam)
            {
                deck.RegenAccumulator = 0f;
                _regeneratingDecks.Remove(uid);
            }
        }
    }

    public bool TrySpendRam(Entity<CyberDeckComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !float.IsFinite(amount) || amount < 0f || ent.Comp.CurrentRam < amount)
            return false;
        ent.Comp.CurrentRam -= amount;
        ent.Comp.RegenAccumulator = 0f;
        Dirty(ent);
        if (ent.Comp.CurrentRam < ent.Comp.MaxRam)
            _regeneratingDecks.Add(ent);
        return true;
    }

    private void Reconcile(Entity<CyberDeckComponent> ent)
    {
        RecalculateRam(ent);
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

        foreach (var module in _modules.GetDirectModules(ent))
        {
            if (!TryComp(module, out CyberDeckScriptComponent? script))
                continue;
            EnsureComp<ActionsContainerComponent>(module);
            if (_actionContainer.EnsureAction(module, ref script.ActionEntity, script.Action))
                _actions.GrantContainedAction(owner, module, script.ActionEntity.Value);
            Dirty(module, script);
        }
        Dirty(ent);
    }

    private void RevokeActions(Entity<CyberDeckComponent> ent)
    {
        if (ent.Comp.GrantedBody is not { } body)
            return;
        if (ent.Comp.OpenActionEntity is { } openAction)
            _actions.RemoveProvidedAction(body, ent.Owner, openAction);
        foreach (var module in _modules.GetDirectModules(ent))
            if (TryComp(module, out CyberDeckScriptComponent? script) && script.ActionEntity is { } action)
                _actions.RemoveProvidedAction(body, module, action);
        ent.Comp.GrantedBody = null;
    }

    private void RecalculateRam(Entity<CyberDeckComponent> ent)
    {
        var maximum = NonNegative(ent.Comp.BaseMaxRam);
        foreach (var module in _modules.GetDirectModules(ent))
            if (TryComp(module, out CyberDeckRamModuleComponent? ram))
                maximum = MathF.Min(float.MaxValue, maximum + NonNegative(ram.RamIncrease));
        var current = NonNegative(ent.Comp.CurrentRam);
        var previousMaximum = NonNegative(ent.Comp.MaxRam);
        var difference = maximum - previousMaximum;
        ent.Comp.MaxRam = maximum;
        ent.Comp.CurrentRam = difference > 0f
            ? MathF.Min(maximum, current + difference)
            : MathF.Min(maximum, current);
        Dirty(ent);
    }

    private bool TryGetExecution(Entity<CyberDeckScriptComponent> script, EntityUid performer, out EntityUid deck, out EntityUid body)
    {
        deck = Transform(script).ParentUid;
        body = _modules.GetInstalledBody(deck) ?? default;
        return body == performer && TryComp<CyberDeckComponent>(deck, out _) && CanUse(deck, performer);
    }

    private bool CanUse(EntityUid deck, EntityUid performer) =>
        _modules.GetInstalledBody(deck) == performer &&
        (!TryComp(deck, out NeuroBandwidthRuntimeComponent? runtime) || runtime.Efficiency > 0f) &&
        !HasComp<Content.Shared.Emp.EmpDisabledComponent>(deck);

    private bool HasRam(Entity<CyberDeckComponent?> ent, float amount) =>
        Resolve(ent, ref ent.Comp, false) && float.IsFinite(amount) && amount >= 0f && ent.Comp.CurrentRam >= amount;

    private void RestoreRam(Entity<CyberDeckComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;
        ent.Comp.CurrentRam = MathF.Min(ent.Comp.MaxRam, ent.Comp.CurrentRam + amount);
        Dirty(ent);
        if (ent.Comp.CurrentRam >= ent.Comp.MaxRam)
            _regeneratingDecks.Remove(ent);
    }

    private static float NonNegative(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;

    private void PopupNoRam(EntityUid body) => _popup.PopupEntity(
        Loc.GetString("cyberdeck-script-not-enough-ram"), body, body, PopupType.SmallCaution);
}

[ByRefEvent]
public record struct CyberDeckScriptExecutionAttemptEvent(
    EntityUid Body,
    EntityUid Performer,
    EntityUid? Target,
    Robust.Shared.Map.EntityCoordinates? Coordinates,
    bool Cancelled = false);
