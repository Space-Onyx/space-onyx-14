// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 RadsammyT <32146976+RadsammyT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Cards.Stack;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Cards;

/// <summary>
/// This handles...
/// </summary>
public sealed partial class CardSpriteSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    public bool TryAdjustLayerQuantity(Entity<SpriteComponent, CardStackComponent> uid, int? cardLimit = null)
    {
        var sprite = uid.Comp1;
        var stack = uid.Comp2;
        var cardCount = cardLimit == null ? stack.Cards.Count : Math.Min(stack.Cards.Count, cardLimit.Value);

        var layerCount = 0;
        //Gets the quantity of layers
        for (var cardIndex = stack.Cards.Count - Math.Max(0, cardCount); cardIndex < stack.Cards.Count; cardIndex++)
        {
            var card = stack.Cards[cardIndex];
            if (!TryComp(card, out SpriteComponent? cardSprite))
                return false;

            layerCount += cardSprite.LayerData.Count;
        }
        layerCount = int.Max(1, layerCount); // Frontier: you need one layer.
        //inserts Missing Layers
        var currentLayerCount = sprite.LayerData.Count;
        if (currentLayerCount < layerCount)
        {
            for (var i = currentLayerCount; i < layerCount; i++)
            {
                _sprite.AddBlankLayer((uid.Owner, sprite), i);
            }
        }
        //Removes extra layers
        else if (currentLayerCount > layerCount)
        {
            for (var i = currentLayerCount - 1; i >= layerCount; i--)
            {
                _sprite.RemoveLayer((uid.Owner, sprite), i);
            }
        }


        return true;
    }

    public bool TryHandleLayerConfiguration(Entity<SpriteComponent, CardStackComponent> uid, int cardCount, Func<Entity<SpriteComponent>, int, int, bool> layerFunc)
    {
        var sprite = uid.Comp1;
        var stack = uid.Comp2;

        // int = index of what card it is from
        List<(int, ISpriteLayer)> layers = [];

        var i = 0;
        for (var cardIndex = Math.Max(0, stack.Cards.Count - Math.Max(0, cardCount)); cardIndex < stack.Cards.Count; cardIndex++)
        {
            var card = stack.Cards[cardIndex];
            if (!TryComp(card, out SpriteComponent? cardSprite))
                return false;
            foreach (var layer in cardSprite.AllLayers)
                layers.Add((i, layer));
            i++;
        }

        var j = 0;
        foreach (var obj in layers)
        {
            var (cardIndex, layer) = obj;
            _sprite.LayerSetVisible((uid.Owner, sprite), j, true);
            _sprite.LayerSetTexture((uid.Owner, sprite), j, layer.Texture);
            _sprite.LayerSetRsiState((uid.Owner, sprite), j, layer.RsiState);
            layerFunc.Invoke((uid, sprite), cardIndex, j);
            j++;
        }

        return true;
    }
}
