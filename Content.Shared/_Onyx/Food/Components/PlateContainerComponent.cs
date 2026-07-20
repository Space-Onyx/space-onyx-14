using System.Numerics;
using Content.Shared.Item;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Food.Components;

[RegisterComponent]
public sealed partial class PlateContainerComponent : Component
{
    public const string ContainerId = "plate-contents";

    [DataField]
    public int MaxItems = 3;

    [DataField]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

    [DataField]
    public HashSet<ProtoId<TagPrototype>> WhitelistTags = new();

    [DataField]
    public HashSet<ProtoId<TagPrototype>> BlacklistTags = new();

    [DataField]
    public HashSet<EntProtoId> WhitelistPrototypes = new();

    [DataField]
    public HashSet<EntProtoId> BlacklistPrototypes = new();

    public TimeSpan NextPopupTime;

    public EntityUid? LastPopupUser;

    [DataField]
    public List<Vector2> ItemOffsets = new()
    {
        new(0f, 0.04f),
        new(-0.12f, 0.02f),
        new(0.12f, 0.02f),
    };
}
