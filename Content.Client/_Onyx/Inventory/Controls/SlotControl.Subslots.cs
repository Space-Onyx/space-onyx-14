// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;

#pragma warning disable IDE0130
namespace Content.Client.UserInterface.Controls;

public abstract partial class SlotControl
{
    protected void SetButtonSize(int size)
    {
        var scale = (float) size / DefaultButtonSize;
        MinSize = new Vector2(size);
        SetSize = new Vector2(size);
        ButtonRect.TextureScale = new Vector2(2f * scale);
        HighlightRect.TextureScale = new Vector2(2f * scale);
        BlockedRect.TextureScale = new Vector2(2f * scale);
        SpriteView.Scale = new Vector2(2f * scale);
        SpriteView.SetSize = new Vector2(size);
        ProtoView.Scale = new Vector2(2f * scale);
        ProtoView.SetSize = new Vector2(size);
        HoverSpriteView.Scale = new Vector2(2f * scale);
        HoverSpriteView.SetSize = new Vector2(size);
        StorageButton.Scale = new Vector2(0.75f * scale);
    }
}
