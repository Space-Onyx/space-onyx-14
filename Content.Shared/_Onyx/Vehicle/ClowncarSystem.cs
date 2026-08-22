using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Clumsy.Components;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class ClowncarComponent : Component
{
    [DataField] public string Container = "clowncar_container";
    [DataField] public int Capacity = 6;
    [DataField] public bool CaptureOnCollide;
    [DataField] public float CollisionVelocity = 3f;
    [DataField] public EntityWhitelist? CrashWhitelist;
    [DataField] public SoundSpecifier LoadSound = default!;
    [DataField] public SoundSpecifier CrashSound = default!;
    [DataField] public EntProtoId ThankAction = "ActionThankDriver";
    [DataField] public EntProtoId QuietAction = "ActionQuietBackThere";
    [ViewVariables] public int ThankCounter;
    [ViewVariables] public EntityUid? QuietActionEntity;
}

public sealed partial class ThankRiderActionEvent : InstantActionEvent;
public sealed partial class QuietBackThereActionEvent : InstantActionEvent;

public sealed partial class ClowncarSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClowncarComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ClowncarComponent, StrapAttemptEvent>(OnStrapAttempt, before: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<ClowncarComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ClowncarComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ClowncarComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ClowncarComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<ClowncarComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<ClowncarComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ClowncarComponent, ThankRiderActionEvent>(OnThank);
        SubscribeLocalEvent<ClowncarComponent, QuietBackThereActionEvent>(OnQuiet);
    }

    private void OnInit(Entity<ClowncarComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private void OnStrapAttempt(Entity<ClowncarComponent> ent, ref StrapAttemptEvent args)
    {
        if (_statusEffects.HasEffectComp<ClumsyCatchStatusEffectComponent>(args.Buckle))
            return;

        args.Cancelled = true;
        if (args.Popup && args.User != null)
            _popup.PopupClient(Loc.GetString("buckle-component-cannot-fit-message"), ent, args.User.Value);
    }

    private void OnInsertAttempt(Entity<ClowncarComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.Container && args.Container.Count >= ent.Comp.Capacity)
            args.Cancel();
    }

    private void OnInserted(Entity<ClowncarComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;
        EnsureComp<StunnedComponent>(args.Entity);
        _actions.AddAction(args.Entity, ent.Comp.ThankAction);
        _audio.PlayPredicted(ent.Comp.LoadSound, ent, args.Entity);
    }

    private void OnRemoved(Entity<ClowncarComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;
        RemovePrototypeAction(args.Entity, ent.Comp.ThankAction);
        RemComp<StunnedComponent>(args.Entity);
    }

    private void OnStrapped(Entity<ClowncarComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.ThankCounter = 0;
        _actions.AddAction(args.Buckle, ref ent.Comp.QuietActionEntity, ent.Comp.QuietAction, ent);
    }

    private void OnUnstrapped(Entity<ClowncarComponent> ent, ref UnstrappedEvent args)
    {
        _actions.RemoveAction(args.Buckle.Owner, ent.Comp.QuietActionEntity);
        ent.Comp.QuietActionEntity = null;
    }

    private void OnCollide(Entity<ClowncarComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurBody.LinearVelocity.Length() < ent.Comp.CollisionVelocity ||
            !_containers.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        if (ent.Comp.CaptureOnCollide && HasComp<HumanoidProfileComponent>(args.OtherEntity) &&
            !HasComp<KnockedDownComponent>(args.OtherEntity) && container.Count < ent.Comp.Capacity)
        {
            _containers.Insert(args.OtherEntity, container);
            return;
        }

        if (ent.Comp.CrashWhitelist != null && !_whitelist.IsWhitelistPass(ent.Comp.CrashWhitelist, args.OtherEntity))
            return;

        foreach (var passenger in container.ContainedEntities.ToArray())
            _containers.Remove(passenger, container);
        _audio.PlayPredicted(ent.Comp.CrashSound, ent, ent.Comp.Operator(ent));
    }

    private void OnThank(Entity<ClowncarComponent> ent, ref ThankRiderActionEvent args)
    {
        if (args.Handled)
            return;
        ent.Comp.ThankCounter++;
        if (ent.Comp.ThankCounter >= 5)
            EjectAll(ent);
        args.Handled = true;
    }

    private void OnQuiet(Entity<ClowncarComponent> ent, ref QuietBackThereActionEvent args)
    {
        ent.Comp.ThankCounter = 0;
        args.Handled = true;
    }

    public void EjectAll(Entity<ClowncarComponent> ent)
    {
        if (!_containers.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;
        ent.Comp.ThankCounter = 0;
        foreach (var passenger in container.ContainedEntities.ToArray())
            _containers.Remove(passenger, container);
    }

    private void RemovePrototypeAction(EntityUid performer, EntProtoId prototype)
    {
        foreach (var (action, _) in _actions.GetActions(performer))
        {
            if (MetaData(action).EntityPrototype is { } actionPrototype && actionPrototype.ID == prototype.Id)
                _actions.RemoveAction(action);
        }
    }
}

internal static class ClowncarExtensions
{
    public static EntityUid? Operator(this ClowncarComponent _, Entity<ClowncarComponent> ent)
    {
        return ent.Owner;
    }
}
