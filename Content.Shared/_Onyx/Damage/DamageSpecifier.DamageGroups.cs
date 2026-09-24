// Content adapted from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under MIT.

using System.Text.Json.Serialization;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    // These fields expose the serialized shape to schema and documentation generators.
    [JsonPropertyName("types")]
    [DataField("types")]
    [UsedImplicitly]
    private Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>? _damageTypeDictionary;

    [JsonPropertyName("groups")]
    [DataField("groups")]
    [UsedImplicitly]
    private Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>? _damageGroupDictionary;
}
