// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Parallax.Biomes.Markers;

public sealed partial class BiomeMarkerLayerPrototype : IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<BiomeMarkerLayerPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }
}
