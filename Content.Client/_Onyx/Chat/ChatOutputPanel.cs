// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.
//
// Layout structure derived from RobustToolbox (https://github.com/space-wizards/RobustToolbox),
// licensed under MIT. Forked into content so examine messages can render their own background box.

using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>
///     Chat output panel that renders messages with their own background box when they contain the
///     <see cref="ExamineBorderTag"/> marker. Layout is a content-side copy of the engine panel so the
///     box can be drawn using the real entry bounds instead of a guessed offset.
/// </summary>
public sealed partial class ChatOutputPanel : Control
{
    public const string StyleClassOutputPanelScrollDownButton = "outputPanelScrollDownButton";
    public const string StylePropertyStyleBox = "stylebox";

    [Dependency] private MarkupTagManager _tagManager = default!;

    private readonly ChatRingBufferList<ChatRichTextEntry> _entries = new();
    private bool _isAtBottom = true;

    private int _totalContentHeight;
    private bool _firstLine = true;
    private StyleBox? _styleBoxOverride;
    private VScrollBar _scrollBar;
    private Button _scrollDownButton;

    public bool ScrollFollowing { get; set; } = true;

    private bool _invalidOnVisible;

    public bool ShowScrollDownButton
    {
        get => _showScrollDownButton;
        set
        {
            _showScrollDownButton = value;
            _updateScrollButtonVisibility();
        }
    }
    private bool _showScrollDownButton;

    public ChatOutputPanel()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;

        _scrollBar = new VScrollBar
        {
            Name = "_v_scroll",
            HorizontalAlignment = HAlignment.Right
        };
        AddChild(_scrollBar);

        AddChild(_scrollDownButton = new Button
        {
            Name = "scrollLiveBtn",
            StyleClasses = { StyleClassOutputPanelScrollDownButton },
            VerticalAlignment = VAlignment.Bottom,
            HorizontalAlignment = HAlignment.Center,
            Text = $"⬇    {Loc.GetString("output-panel-scroll-down-button-text")}    ⬇",
            MaxWidth = 300,
            Visible = false,
        });

        _scrollDownButton.OnPressed += _ => ScrollToBottom();

        _scrollBar.OnValueChanged += _ =>
        {
            _isAtBottom = _scrollBar.IsAtEnd;
            _updateScrollButtonVisibility();
        };
    }

    public int EntryCount => _entries.Count;

    public StyleBox? StyleBoxOverride
    {
        get => _styleBoxOverride;
        set
        {
            _styleBoxOverride = value;
            InvalidateMeasure();
            _invalidateEntries();
        }
    }

    public void Clear()
    {
        _firstLine = true;

        foreach (var entry in _entries)
        {
            entry.RemoveControls();
        }

        _entries.Clear();
        _totalContentHeight = 0;
        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        _scrollBar.Value = 0;
    }

    public FormattedMessage GetMessage(Index index)
    {
        return new FormattedMessage(_entries[index].Message);
    }

    public void RemoveEntry(Index index)
    {
        var entry = _entries[index];
        entry.RemoveControls();
        _entries.RemoveAt(index.GetOffset(_entries.Count));

        var font = _getFont();
        _totalContentHeight -= entry.Height + font.GetLineSeparation(UIScale);
        if (_entries.Count == 0)
        {
            Clear();
        }

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
    }

    public void AddText(string text)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        AddMessage(msg);
    }

    public void AddMessage(FormattedMessage message, Color? defaultColor = null)
    {
        AddMessage(message, ChatRichTextEntry.DefaultTags, defaultColor);
    }

    public void AddMessage(FormattedMessage message, Type[]? tagsAllowed, Color? defaultColor = null)
    {
        var entry = new ChatRichTextEntry(message, this, _tagManager, tagsAllowed, defaultColor);

        entry.Update(_tagManager, _getFont(), _getContentBox().Width, UIScale);

        _entries.Add(entry);
        var font = _getFont();
        AddNewItemHeight(font, entry);

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        if (_isAtBottom && ScrollFollowing)
        {
            _scrollBar.MoveToEnd();
        }
    }

    public void SetMessage(Index index, FormattedMessage message, Color? defaultColor = null)
    {
        SetMessage(index, message, ChatRichTextEntry.DefaultTags, defaultColor);
    }

    public void SetMessage(Index index, FormattedMessage message, Type[]? tagsAllowed, Color? defaultColor = null)
    {
        var atBottom = !_scrollDownButton.Visible;
        var oldEntry = _entries[index];
        var font = _getFont();
        _totalContentHeight -= oldEntry.Height + font.GetLineSeparation(UIScale);
        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);

        var entry = new ChatRichTextEntry(message, this, _tagManager, tagsAllowed, defaultColor);
        entry.Update(_tagManager, _getFont(), _getContentBox().Width, UIScale);
        _entries[index] = entry;

        AddNewItemHeight(font, entry);

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        if (atBottom)
            _scrollBar.Value = _scrollBar.MaxValue;
    }

    private void AddNewItemHeight(Font font, in ChatRichTextEntry entry)
    {
        _totalContentHeight += entry.Height;
        if (_firstLine)
        {
            _firstLine = false;
        }
        else
        {
            _totalContentHeight += font.GetLineSeparation(UIScale);
        }
    }

    public void ScrollToBottom()
    {
        _scrollBar.MoveToEnd();
        _isAtBottom = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var style = _getStyleBox();
        var font = _getFont();
        var lineSeparation = font.GetLineSeparation(UIScale);
        style?.Draw(handle, PixelSizeBox, UIScale);
        var contentBox = _getContentBox();

        var entryOffset = -_scrollBar.Value;

        var context = new MarkupDrawingContext(2);

        foreach (ref var entry in _entries)
        {
            if (entryOffset + entry.Height < 0)
            {
                entry.HideControls();
                entryOffset += entry.Height + lineSeparation;
                continue;
            }

            if (entryOffset > contentBox.Height)
            {
                entry.HideControls();
                continue;
            }

            entry.Draw(_tagManager, handle, font, contentBox, entryOffset, context, UIScale);

            entryOffset += entry.Height + lineSeparation;
        }
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (MathHelper.CloseToPercent(0, args.Delta.Y))
        {
            return;
        }

        _scrollBar.ValueTarget -= _getScrollSpeed() * args.Delta.Y;
    }

    protected override void Resized()
    {
        base.Resized();

        var styleBoxSize = _getStyleBox()?.MinimumSize.Y ?? 0;

        _scrollBar.Page = UIScale * (Height - styleBoxSize);
        _updateScrollButtonVisibility();
        _invalidateEntries();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return _getStyleBox()?.MinimumSize ?? Vector2.Zero;
    }

    private void _invalidateEntries()
    {
        _totalContentHeight = 0;
        var font = _getFont();
        var sizeX = _getContentBox().Width;
        foreach (ref var entry in _entries)
        {
            entry.Update(_tagManager, font, sizeX, UIScale);
            _totalContentHeight += entry.Height + font.GetLineSeparation(UIScale);
        }

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        if (_isAtBottom && ScrollFollowing)
        {
            _scrollBar.MoveToEnd();
        }
    }

    private Font _getFont()
    {
        if (TryGetStyleProperty<Font>("font", out var font))
        {
            return font;
        }

        return UserInterfaceManager.ThemeDefaults.DefaultFont;
    }

    private StyleBox? _getStyleBox()
    {
        if (StyleBoxOverride != null)
        {
            return StyleBoxOverride;
        }

        TryGetStyleProperty<StyleBox>(StylePropertyStyleBox, out var box);
        return box;
    }

    private float _getScrollSpeed()
    {
        return GetScrollSpeed(_getFont(), UIScale);
    }

    private UIBox2 _getContentBox()
    {
        var style = _getStyleBox();
        var box = style?.GetContentBox(PixelSizeBox, UIScale) ?? PixelSizeBox;
        box.Right = Math.Max(box.Left, box.Right - _scrollBar.DesiredPixelSize.X);
        return box;
    }

    protected override void UIScaleChanged()
    {
        if (!VisibleInTree)
            _invalidOnVisible = true;
        else
            _invalidateEntries();

        base.UIScaleChanged();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();

        _invalidateEntries();
    }

    internal static float GetScrollSpeed(Font font, float scale)
    {
        return font.GetLineHeight(scale) * 2;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _invalidateEntries();
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        if (newVisible && _invalidOnVisible)
        {
            _invalidateEntries();
            _invalidOnVisible = false;
        }
    }

    private void _updateScrollButtonVisibility()
    {
        _scrollDownButton.Visible = ShowScrollDownButton && !_isAtBottom;
    }
}
