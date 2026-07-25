using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent]
public sealed partial class ClothingAutoInjectComponent : Component
{
    [DataField(required: true)] public Dictionary<string, FixedPoint2> Reagents = new();
    [DataField] public bool AutoInjectOnCrit = true;
    [DataField] public bool AutoInjectOnAbility;
    [DataField] public TimeSpan AutoInjectInterval = TimeSpan.FromSeconds(120);
    public TimeSpan NextAutoInjectTime;
    [DataField] public EntProtoId Action = "ActionActivateAutoinjector";
    [DataField] public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
    [DataField] public LocId Popup = "autoinjector-injection-hardsuit";
    public EntityUid? ActionEntity;
}
