using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Trigger;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpawnTableOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField(required: true)]
    public EntityTableSelector Table = default!;

    [DataField, AutoNetworkedField]
    public bool UseMapCoords;

    [DataField, AutoNetworkedField]
    public bool Predicted;
}
