using System.Diagnostics.CodeAnalysis;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.AbstractAnalyzer;

public abstract partial class AbstractAnalyzerSystem<TAnalyzerComponent, TAnalyzerDoAfterEvent> : EntitySystem
    where TAnalyzerComponent : AbstractAnalyzerComponent
    where TAnalyzerDoAfterEvent : SimpleDoAfterEvent, new()
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private IDynamicTypeFactory _typeFactory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TAnalyzerComponent, TAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<TAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<TAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<TAnalyzerComponent, DroppedEvent>(OnDropped);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var transform))
        {
            if (component.NextUpdate > _timing.CurTime || component.ScannedEntity is not { } target)
                continue;

            if (Deleted(target))
            {
                StopAnalyzingEntity((uid, component), target);
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;
            if (component.MaxScanRange is { } range &&
                !_transformSystem.InRange(Transform(target).Coordinates, transform.Coordinates, range))
            {
                StopAnalyzingEntity((uid, component), target);
                continue;
            }

            UpdateScannedUser(uid, target, true);
        }
    }

    private void OnAfterInteract(Entity<TAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !ValidScanTarget(args.Target) ||
            !_cell.HasDrawCharge(ent.Owner, user: args.User))
            return;

        _audio.PlayPredicted(ent.Comp.ScanningBeginSound, ent, null);
        var ev = _typeFactory.CreateInstance<TAnalyzerDoAfterEvent>();
        var cancelled = !_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.ScanDelay,
            ev,
            ent,
            target: args.Target,
            used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || cancelled || ent.Comp.Silent || args.Target is null)
            return;

        if (ScanTargetPopupMessage(ent, args, out var message))
            _popupSystem.PopupEntity(message, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    private void OnDoAfter(Entity<TAnalyzerComponent> ent, ref TAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null ||
            !_cell.HasDrawCharge(ent.Owner, user: args.User))
            return;

        if (!ent.Comp.Silent)
            _audio.PlayPredicted(ent.Comp.ScanningEndSound, ent, null);

        if (_uiSystem.HasUi(ent.Owner, GetUiKey()))
            _uiSystem.OpenUi(ent.Owner, GetUiKey(), args.User);

        ent.Comp.ScannedEntity = args.Target.Value;
        _toggle.TryActivate(ent.Owner);
        UpdateScannedUser(ent.Owner, args.Target.Value, true);
        args.Handled = true;
    }

    private void OnInsertedIntoContainer(Entity<TAnalyzerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnToggled(Entity<TAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } target)
            StopAnalyzingEntity(ent, target);
    }

    private void OnDropped(Entity<TAnalyzerComponent> ent, ref DroppedEvent args)
    {
        if (ent.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(ent.Owner);
    }

    private void StopAnalyzingEntity(Entity<TAnalyzerComponent> analyzer, EntityUid target)
    {
        analyzer.Comp.ScannedEntity = null;
        _toggle.TryDeactivate(analyzer.Owner);
        UpdateScannedUser(analyzer.Owner, target, false);
    }

    public abstract void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode);
    protected abstract Enum GetUiKey();
    protected abstract bool ScanTargetPopupMessage(Entity<TAnalyzerComponent> uid,
        AfterInteractEvent args,
        [NotNullWhen(true)] out string? message);
    protected abstract bool ValidScanTarget(EntityUid? target);
}
