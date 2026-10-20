// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.IconSmoothing;
using Content.Client.IconSmoothing.Smoothings;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.IconSmoothing;

/// <summary>
/// Smooths the first sprite layer using legacy RSI flags: north=1, south=2, east=4, west=8.
/// </summary>
public sealed partial class CardinalFlagsSpriteSmoothing : CornerSpriteSmoothing
{
    public override void InitializeStates(Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        sprite.LayerMapSet(entity.AsNullable(), LayerKey, 0);
    }

    public override IEnumerable<SpriteLayer> EnumerateStates(HashSet<string>?[] layers, Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        var flags = 0;
        if (layers[0] is { } south && south.Overlaps(Mask))
            flags |= 2;
        if (layers[2] is { } east && east.Overlaps(Mask))
            flags |= 4;
        if (layers[4] is { } north && north.Overlaps(Mask))
            flags |= 1;
        if (layers[6] is { } west && west.Overlaps(Mask))
            flags |= 8;

        yield return new SpriteLayer(LayerKey, Base + flags);
    }
}
