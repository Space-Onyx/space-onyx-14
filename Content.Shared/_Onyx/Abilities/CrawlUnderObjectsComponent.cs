using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Abilities;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCrawlUnderObjectsSystem))]
public sealed partial class CrawlUnderObjectsComponent : Component
{
    [DataField]
    public EntityUid? ToggleAction;

    [DataField]
    public EntProtoId? ActionProto;

    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public List<(string Key, int OriginalMask)> ChangedFixtures = [];

    [ViewVariables]
    public int? OriginalDrawDepth;

    [DataField]
    public float SpeedModifier = 0.7f;
}

public sealed partial class ToggleCrawlingStateEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum CrawlUnderObjectsVisuals : byte
{
    Enabled,
}
