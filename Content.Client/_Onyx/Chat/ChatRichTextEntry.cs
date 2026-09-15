// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.
//
// Layout logic derived from RobustToolbox (https://github.com/space-wizards/RobustToolbox),
// licensed under MIT. Extended for Space Onyx with the examine-background box.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Content.Client.Stylesheets.Palette;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Collections;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>
///     Rich text entry used by <see cref="ChatOutputPanel"/>. Behaves like the engine entry, but when
///     the message contains the <see cref="ExamineBorderTag"/> marker it paints its own background box.
/// </summary>
internal struct ChatRichTextEntry
{
    public static readonly Type[] DefaultTags =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag)
    ];

    private const float BoxPadding = 6f;

    private static readonly Color BoxFill = Palettes.Slate.BackgroundDark;
    private static readonly Color BoxBorder = Palettes.Slate.Element;

    private readonly Color _defaultColor;
    private readonly Type[]? _tagsAllowed;

    public readonly FormattedMessage Message;

    public int Height;
    public int Width;
    public ValueList<int> LineBreaks;

    public bool IsInBox;

    public readonly Dictionary<int, Control>? Controls;

    public ChatRichTextEntry(
        FormattedMessage message,
        Control parent,
        MarkupTagManager tagManager,
        Color? defaultColor = null) : this(message, parent, tagManager, DefaultTags, defaultColor)
    {
    }

    public ChatRichTextEntry(
        FormattedMessage message,
        Control parent,
        MarkupTagManager tagManager,
        Type[]? tagsAllowed,
        Color? defaultColor = null)
    {
        Message = message;
        Height = 0;
        Width = 0;
        LineBreaks = default;
        IsInBox = false;
        _defaultColor = defaultColor ?? new Color(200, 200, 200);
        _tagsAllowed = tagsAllowed;
        Controls = GetControls(parent, tagManager);

        foreach (var node in Message)
        {
            if (node.Name == ExamineBorderTag.TagName)
            {
                IsInBox = true;
                break;
            }
        }
    }

    private readonly Dictionary<int, Control>? GetControls(Control parent, MarkupTagManager tagManager)
    {
        Dictionary<int, Control>? tagControls = null;
        var nodeIndex = -1;

        foreach (var node in Message)
        {
            nodeIndex++;

            if (node.Name == null)
                continue;

            if (!tagManager.TryGetMarkupTagHandler(node.Name, _tagsAllowed, out var handler) || !handler.TryCreateControl(node, out var control))
                continue;

            DebugTools.Assert(handler.TryCreateControl(node, out var other) && other != control);

            parent.Children.Add(control);
            tagControls ??= new Dictionary<int, Control>();
            tagControls.Add(nodeIndex, control);
        }

        return tagControls;
    }

    public readonly void RemoveControls()
    {
        if (Controls == null)
            return;

        foreach (var ctrl in Controls.Values)
        {
            ctrl.Orphan();
        }
    }

    public ChatRichTextEntry Update(MarkupTagManager tagManager, Font defaultFont, float maxSizeX, float uiScale, float lineHeightScale = 1)
    {
        Height = defaultFont.GetHeight(uiScale);
        LineBreaks.Clear();

        if (IsInBox && maxSizeX > 0)
            maxSizeX = MathF.Max(1f, maxSizeX - BoxPadding * 2f * uiScale);

        int? breakLine;
        var wordWrap = new ChatWordWrap(maxSizeX);
        var context = new MarkupDrawingContext();
        context.Font.Push(defaultFont);
        context.Color.Push(_defaultColor);

        var nodeIndex = -1;
        foreach (var node in Message)
        {
            nodeIndex++;
            var text = ProcessNode(tagManager, node, context);

            if (!context.Font.TryPeek(out var font))
                font = defaultFont;

            foreach (var rune in text.EnumerateRunes())
            {
                if (ProcessRune(ref this, rune, out breakLine))
                    continue;

                if (!font.TryGetCharMetrics(rune, uiScale, out var metrics))
                    continue;

                if (ProcessMetric(ref this, metrics, out breakLine))
                    return this;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            control.Measure(new Vector2(maxSizeX, Height));

            var desiredSize = control.DesiredPixelSize;
            var controlMetrics = new CharMetrics(
                0, 0,
                desiredSize.X,
                desiredSize.X,
                desiredSize.Y);

            if (ProcessMetric(ref this, controlMetrics, out breakLine))
                return this;
        }

        Width = wordWrap.FinalizeText(out breakLine);
        CheckLineBreak(ref this, breakLine);

        return this;

        bool ProcessRune(ref ChatRichTextEntry src, Rune rune, out int? outBreakLine)
        {
            wordWrap.NextRune(rune, out breakLine, out var breakNewLine, out var skip);
            CheckLineBreak(ref src, breakLine);
            CheckLineBreak(ref src, breakNewLine);
            outBreakLine = breakLine;
            return skip;
        }

        bool ProcessMetric(ref ChatRichTextEntry src, CharMetrics metrics, out int? outBreakLine)
        {
            wordWrap.NextMetrics(metrics, out breakLine, out var abort);
            CheckLineBreak(ref src, breakLine);
            outBreakLine = breakLine;
            return abort;
        }

        void CheckLineBreak(ref ChatRichTextEntry src, int? line)
        {
            if (line is { } l)
            {
                src.LineBreaks.Add(l);
                if (!context.Font.TryPeek(out var font))
                    font = defaultFont;

                src.Height += GetLineHeight(font, uiScale, lineHeightScale);
            }
        }
    }

    internal readonly void HideControls()
    {
        if (Controls == null)
            return;

        foreach (var control in Controls.Values)
        {
            control.Visible = false;
        }
    }

    public readonly void Draw(
        MarkupTagManager tagManager,
        DrawingHandleBase handle,
        Font defaultFont,
        UIBox2 drawBox,
        float verticalOffset,
        MarkupDrawingContext context,
        float uiScale,
        float lineHeightScale = 1,
        TextOutline? outline = null)
    {
        if (IsInBox && handle is DrawingHandleScreen screenHandle)
        {
            var lineSeparation = defaultFont.GetLineSeparation(uiScale);
            var verticalPadding = MathF.Min(BoxPadding * uiScale, lineSeparation);
            var top = drawBox.Top + verticalOffset - verticalPadding;
            var bottom = drawBox.Top + verticalOffset + Height + verticalPadding;
            var box = new UIBox2(drawBox.Left, top, drawBox.Right, bottom);
            screenHandle.DrawRect(box, BoxFill);
            screenHandle.DrawRect(box, BoxBorder, false);
        }

        if (outline is { } outlineSettings)
        {
            DrawPass(
                tagManager,
                handle,
                defaultFont,
                drawBox,
                verticalOffset,
                context,
                uiScale,
                lineHeightScale,
                outlineSettings,
                arrangeControls: false);
        }

        DrawPass(
            tagManager,
            handle,
            defaultFont,
            drawBox,
            verticalOffset,
            context,
            uiScale,
            lineHeightScale,
            outline: null,
            arrangeControls: true);
    }

    private readonly void DrawPass(
        MarkupTagManager tagManager,
        DrawingHandleBase handle,
        Font defaultFont,
        UIBox2 drawBox,
        float verticalOffset,
        MarkupDrawingContext context,
        float uiScale,
        float lineHeightScale,
        TextOutline? outline,
        bool arrangeControls)
    {
        context.Clear();
        context.Color.Push(_defaultColor);
        context.Font.Push(defaultFont);

        var inset = IsInBox ? BoxPadding * uiScale : 0f;
        var globalBreakCounter = 0;
        var lineBreakIndex = 0;
        var baseLine = drawBox.TopLeft + new Vector2(inset, defaultFont.GetAscent(uiScale) + verticalOffset);
        var controlYAdvance = 0f;
        var hasOutline = outline.HasValue;
        var outlineSettings = outline.GetValueOrDefault();

        var spaceRune = new Rune(' ');

        var nodeIndex = -1;
        foreach (var node in Message)
        {
            nodeIndex++;
            var text = ProcessNode(tagManager, node, context);
            if (!context.Color.TryPeek(out var color) || !context.Font.TryPeek(out var font))
            {
                color = _defaultColor;
                font = defaultFont;
            }

            foreach (var rune in text.EnumerateRunes())
            {
                bool skipSpaceBaseline = false;

                if (lineBreakIndex < LineBreaks.Count &&
                    LineBreaks[lineBreakIndex] == globalBreakCounter)
                {
                    baseLine = new Vector2(drawBox.Left + inset, baseLine.Y + GetLineHeight(font, uiScale, lineHeightScale) + controlYAdvance);
                    controlYAdvance = 0;
                    lineBreakIndex += 1;

                    if (rune == spaceRune)
                        skipSpaceBaseline = true;
                }

                var advance = hasOutline
                    ? font.DrawCharOutline(handle, rune, baseLine, uiScale, outlineSettings)
                    : font.DrawChar(handle, rune, baseLine, uiScale, color);

                if (!skipSpaceBaseline)
                    baseLine += new Vector2(advance, 0);

                globalBreakCounter += 1;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            var invertedScale = 1f / uiScale;
            if (arrangeControls)
            {
                control.Visible = true;
                control.Measure(new Vector2(Width, Height));
                control.Arrange(UIBox2.FromDimensions(
                    baseLine.X * invertedScale,
                    (baseLine.Y - defaultFont.GetAscent(uiScale)) * invertedScale,
                    control.DesiredSize.X,
                    control.DesiredSize.Y
                ));
            }
            else
            {
                control.Measure(new Vector2(Width, Height));
            }

            var advanceX = control.DesiredPixelSize.X;
            controlYAdvance = Math.Max(0f, (control.DesiredPixelSize.Y - GetLineHeight(font, uiScale, lineHeightScale)) * invertedScale);
            baseLine += new Vector2(advanceX, 0);
        }
    }

    private readonly string ProcessNode(MarkupTagManager tagManager, MarkupNode node, MarkupDrawingContext context)
    {
        if (node.Name == null)
            return node.Value.StringValue ?? "";

        if (!tagManager.TryGetMarkupTagHandler(node.Name, _tagsAllowed, out var tag))
            return "";

        if (!node.Closing)
        {
            tag.PushDrawContext(node, context);
            return tag.TextBefore(node);
        }

        tag.PopDrawContext(node, context);
        return tag.TextAfter(node);
    }

    private static int GetLineHeight(Font font, float uiScale, float lineHeightScale)
    {
        var height = font.GetLineHeight(uiScale);
        return (int)(height * lineHeightScale);
    }
}
