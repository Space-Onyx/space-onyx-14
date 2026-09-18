// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.PDA;

/// <summary>
/// Narrow sidebar tab with a rotated label. Selected tab gets a left
/// accent strip and bright text, others stay dim with no underline.
/// </summary>
public sealed class PdaSidebarButton : ContainerButton
{
    private readonly PanelContainer _accent = new()
    {
        MinSize = new(2, 0),
    };

    private readonly RotatedLabel _label = new()
    {
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment = VAlignment.Center,
        HorizontalExpand = true,
        VerticalExpand = true,
    };

    private readonly AnimatedTextureRect _icon = new()
    {
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment = VAlignment.Center,
        HorizontalExpand = true,
        VerticalExpand = true,
        Visible = false,
    };

    private bool _current;
    private Color _accentColor = Color.FromHex("#6B9A88");
    private static readonly Color ActiveText = Color.White;
    private static readonly Color InactiveText = Color.FromHex("#84918C");

    public string ButtonText
    {
        get => _label.Text ?? string.Empty;
        set
        {
            _label.Text = value;
            _label.Visible = true;
            _icon.Visible = false;
        }
    }

    public SpriteSpecifier? IconTexture
    {
        set
        {
            if (value is null)
            {
                _icon.Visible = false;
                _label.Visible = true;
                return;
            }

            _icon.SetFromSpriteSpecifier(value);
            _icon.Visible = true;
            _label.Visible = false;
        }
    }

    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            Refresh();
        }
    }

    public bool IsCurrent
    {
        get => _current;
        set
        {
            _current = value;
            Refresh();
        }
    }

    public PdaSidebarButton()
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        box.AddChild(_accent);
        box.AddChild(_label);
        box.AddChild(_icon);
        AddChild(box);
        Refresh();
    }

    private void Refresh()
    {
        if (_accent.PanelOverride is not StyleBoxFlat accentBox)
        {
            accentBox = new StyleBoxFlat { BorderThickness = new Thickness(0) };
            _accent.PanelOverride = accentBox;
        }

        accentBox.BackgroundColor = _current ? _accentColor : Color.Transparent;
        _label.FontColorOverride = _current ? ActiveText : InactiveText;
        _icon.Modulate = _current ? ActiveText : InactiveText;
    }
}
