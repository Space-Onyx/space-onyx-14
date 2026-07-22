using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Weapons.AmmoSelector;

[Prototype]
public sealed partial class SelectableAmmoPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField(required: true)]
    public string Desc = default!;

    [DataField(required: true)]
    public EntProtoId ProtoId;

    [DataField]
    public Color? Color;

    [DataField]
    public float FireCost = 100f;
}
