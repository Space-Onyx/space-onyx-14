using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.DoAfter;

namespace Content.Shared._Onyx.Xenobiology.Slimes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class XenobioSlimeComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Breed;

    [DataField(required: true), AutoNetworkedField]
    public LocId BreedName;

    [DataField, AutoNetworkedField]
    public EntProtoId? ProducedExtract;

    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId> PotentialMutations = new();

    [DataField, AutoNetworkedField]
    public int MinOffspring = 1;

    [DataField, AutoNetworkedField]
    public int MaxOffspring = 4;

    [DataField, AutoNetworkedField]
    public int ExtractsProduced = 1;

    [DataField, AutoNetworkedField]
    public float MutationChance = 0.45f;

    [DataField, AutoNetworkedField]
    public float MitosisHunger = 125f;

    [DataField, AutoNetworkedField]
    public float JitterDifference = 25f;

    [DataField, AutoNetworkedField]
    public string? Shader;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? Tamer;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? LatchedTarget;

    [ViewVariables]
    public DoAfterId? LastLatchDoAfterId;

    [ViewVariables]
    public bool LastLatchSucceeded;

    [DataField]
    public int MaxContainedEntities = 1;

    [DataField]
    public EntProtoId TameEffect = "EffectHearts";

    [DataField]
    public TimeSpan MitosisInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextMitosis;

    [DataField]
    public TimeSpan LatchDuration = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan OnReleaseStunDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier MitosisSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    [DataField]
    public SoundSpecifier EatSound = new SoundPathSpecifier("/Audio/Voice/Talk/slime.ogg");
}

[RegisterComponent]
public sealed partial class RandomizeXenobioSlimeComponent : Component;

[Serializable, NetSerializable]
public enum XenobioSlimeVisuals : byte
{
    Color,
    Shader,
}
