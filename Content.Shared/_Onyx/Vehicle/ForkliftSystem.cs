using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Vehicle;

[RegisterComponent]
public sealed partial class ForkliftComponent : Component
{
    [DataField] public int Capacity = 4;
    [DataField] public SoundSpecifier LiftSound = default!;
    [ViewVariables] public EntityUid? LiftAction;
    [ViewVariables] public EntityUid? DropAction;
    [ViewVariables] public EntityUid? LiftSoundUid;
    [ViewVariables] public TimeSpan? LiftSoundEndTime;
}

[Serializable, NetSerializable]
public enum ForkliftVisuals : byte
{
    CrateState,
}

[Serializable, NetSerializable]
public enum ForkliftCrateState : byte
{
    Empty,
    OneCrate,
    TwoCrates,
    ThreeCrates,
    FourCrates,
}

public sealed partial class ForkliftActionEvent : EntityTargetActionEvent;
public sealed partial class UnforkliftActionEvent : InstantActionEvent;

public sealed partial class ForkliftSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string StorageId = "crate_storage";
    private static readonly EntProtoId LiftActionId = "ActionForklift";
    private static readonly EntProtoId DropActionId = "ActionUnforklift";
    private static readonly ProtoId<TagPrototype> CrateTag = "Crate";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ForkliftComponent, ComponentInit>(OnContainerChanged);
        SubscribeLocalEvent<ForkliftComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ForkliftComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ForkliftComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<ForkliftComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<ForkliftActionEvent>(OnLift);
        SubscribeLocalEvent<ForkliftComponent, UnforkliftActionEvent>(OnDrop);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ForkliftComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.LiftSoundEndTime == null || _timing.CurTime < comp.LiftSoundEndTime)
                continue;

            if (comp.LiftSoundUid is { } sound)
                _audio.Stop(sound);
            comp.LiftSoundUid = null;
            comp.LiftSoundEndTime = null;
        }
    }

    private void OnStrapped(Entity<ForkliftComponent> ent, ref StrappedEvent args)
    {
        _actions.AddAction(args.Buckle, ref ent.Comp.LiftAction, LiftActionId, ent);
        _actions.AddAction(args.Buckle, ref ent.Comp.DropAction, DropActionId, ent);
    }

    private void OnUnstrapped(Entity<ForkliftComponent> ent, ref UnstrappedEvent args)
    {
        _actions.RemoveAction(args.Buckle.Owner, ent.Comp.LiftAction);
        _actions.RemoveAction(args.Buckle.Owner, ent.Comp.DropAction);
        ent.Comp.LiftAction = null;
        ent.Comp.DropAction = null;
    }

    private void OnLift(ForkliftActionEvent args)
    {
        if (args.Handled || args.Action.Comp.Container is not { } forklift ||
            !TryComp(forklift, out ForkliftComponent? comp) ||
            !_containers.TryGetContainer(forklift, StorageId, out var container) ||
            container.Count >= comp.Capacity || !_tags.HasTag(args.Target, CrateTag))
            return;

        if (!_containers.Insert(args.Target, container))
            return;

        PlaySound(args.Performer, args.Action, (forklift, comp));
        args.Handled = true;
    }

    private void OnDrop(Entity<ForkliftComponent> ent, ref UnforkliftActionEvent args)
    {
        if (args.Handled || !_containers.TryGetContainer(ent, StorageId, out var container) || container.Count == 0)
            return;

        var target = Transform(ent).Coordinates.Offset(Transform(ent).LocalRotation.GetDir().ToVec());
        if (!_containers.Remove(container.ContainedEntities.First(), container, destination: target))
            return;

        PlaySound(args.Performer, args.Action, ent);
        args.Handled = true;
    }

    private void PlaySound(EntityUid user, Entity<ActionComponent> action, Entity<ForkliftComponent> forklift)
    {
        if (forklift.Comp.LiftSoundUid != null)
            return;

        var sound = _audio.PlayPredicted(forklift.Comp.LiftSound, forklift, user, forklift.Comp.LiftSound.Params);
        if (sound == null || action.Comp.UseDelay == null)
            return;

        forklift.Comp.LiftSoundUid = sound.Value.Entity;
        forklift.Comp.LiftSoundEndTime = _timing.CurTime + action.Comp.UseDelay.Value;
    }

    private void OnContainerChanged<T>(Entity<ForkliftComponent> ent, ref T args)
    {
        if (!_containers.TryGetContainer(ent, StorageId, out var container))
            return;

        var state = container.Count switch
        {
            0 => ForkliftCrateState.Empty,
            1 => ForkliftCrateState.OneCrate,
            2 => ForkliftCrateState.TwoCrates,
            3 => ForkliftCrateState.ThreeCrates,
            _ => ForkliftCrateState.FourCrates,
        };
        _appearance.SetData(ent, ForkliftVisuals.CrateState, state);
    }
}
