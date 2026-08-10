using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Vehicle;

[RegisterComponent]
public sealed partial class VehicleHornComponent : Component
{
    [DataField(required: true)] public SoundSpecifier Sound = default!;
    [ViewVariables] public EntityUid? Action;
}

public sealed partial class VehicleHornActionEvent : InstantActionEvent;

public sealed partial class VehicleHornSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly EntProtoId HornActionId = "ActionVehicleHorn";

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleHornComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleHornComponent, VehicleHornActionEvent>(OnHorn);
    }

    private void OnOperatorSet(Entity<VehicleHornComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldOperator)
            _actions.RemoveAction(oldOperator, ent.Comp.Action);

        if (args.NewOperator is { } newOperator)
            _actions.AddAction(newOperator, ref ent.Comp.Action, HornActionId, ent);
    }

    private void OnHorn(Entity<VehicleHornComponent> ent, ref VehicleHornActionEvent args)
    {
        if (args.Handled || args.Action.Comp.Container != ent.Owner ||
            !TryComp<VehicleComponent>(ent, out var vehicle) || vehicle.Operator != args.Performer)
            return;

        _audio.PlayPredicted(ent.Comp.Sound, ent, args.Performer);
        args.Handled = true;
    }
}
