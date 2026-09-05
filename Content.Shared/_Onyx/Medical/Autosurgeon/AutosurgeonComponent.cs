using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical.Autosurgeon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutosurgeonComponent : Component
{
    [DataField(required: true)]
    public BodyPartType TargetPart;

    [DataField(required: true)]
    public BodyPartSymmetry TargetSymmetry;

    [DataField(required: true)]
    public EntProtoId Replacement;

    [DataField]
    public EntProtoId? ChildReplacement;

    /// <summary>
    /// Organ slot to replace. Null means Replacement is a body part.
    /// </summary>
    [DataField]
    public string? TargetOrgan;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public bool Used;

    [AutoNetworkedField]
    public bool InUse;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Onyx/Machines/autosurgeon.ogg");

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActiveSound;
}

[Serializable, NetSerializable]
public sealed partial class AutosurgeonDoAfterEvent : SimpleDoAfterEvent;
