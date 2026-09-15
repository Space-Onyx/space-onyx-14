// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.
//
// Layout helper derived from RobustToolbox (https://github.com/space-wizards/RobustToolbox),
// licensed under MIT. Renamed for the Onyx chat renderer.

using System;
using System.Diagnostics.Contracts;
using System.Text;
using Robust.Client.Graphics;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>
///     Helper utility struct for word-wrapping calculations.
/// </summary>
internal struct ChatWordWrap
{
    private readonly float _maxSizeX;

    public float MaxUsedWidth;
    // Index we put into the LineBreaks list when a line break should occur.
    public int BreakIndexCounter;
    public int NextBreakIndexCounter;
    // If the CURRENT processing word ends up too long, this is the index to put a line break.
    public (int index, float lineSize)? WordStartBreakIndex;
    // Word size in pixels.
    public int WordSizePixels;
    // The horizontal position of the text cursor.
    public int PosX;
    public Rune LastRune;

    public ChatWordWrap(float maxSizeX)
    {
        this = default;
        _maxSizeX = maxSizeX;
        LastRune = new Rune('A');
    }

    public void NextRune(Rune rune, out int? breakLine, out int? breakNewLine, out bool skip)
    {
        BreakIndexCounter = NextBreakIndexCounter;
        NextBreakIndexCounter += 1;

        breakLine = null;
        breakNewLine = null;
        skip = false;

        if (IsWordBoundary(LastRune, rune) || rune == new Rune('\n'))
        {
            if (PosX > _maxSizeX && LastRune != new Rune(' '))
            {
                DebugTools.Assert(WordStartBreakIndex.HasValue,
                    "wordStartBreakIndex can only be null if the word begins at a new line, in which case this branch shouldn't be reached as the word would be split due to being longer than a single line.");
                if (!WordStartBreakIndex.HasValue)
                    return;

                breakLine = WordStartBreakIndex!.Value.index;
                MaxUsedWidth = Math.Max(MaxUsedWidth, WordStartBreakIndex.Value.lineSize);
                PosX = WordSizePixels;
            }

            WordSizePixels = 0;
            WordStartBreakIndex = (BreakIndexCounter, PosX);

            if (rune == new Rune('\n'))
            {
                MaxUsedWidth = Math.Max(MaxUsedWidth, PosX);
                PosX = 0;
                WordStartBreakIndex = null;
                skip = true;
                breakNewLine = BreakIndexCounter;
            }
        }

        LastRune = rune;
    }

    public void NextMetrics(in CharMetrics metrics, out int? breakLine, out bool abort)
    {
        abort = false;
        breakLine = null;

        var oldWordSizePixels = WordSizePixels;
        WordSizePixels += metrics.Advance;
        PosX += metrics.Advance;

        if (PosX <= _maxSizeX)
            return;

        if (WordStartBreakIndex.HasValue && oldWordSizePixels != 0)
        {
            breakLine = WordStartBreakIndex.Value.index;
            MaxUsedWidth = Math.Max(MaxUsedWidth, WordStartBreakIndex.Value.lineSize);
            PosX = WordSizePixels;
        }

        if (WordSizePixels > _maxSizeX)
        {
            if (oldWordSizePixels == 0)
            {
                abort = true;
                return;
            }

            breakLine = BreakIndexCounter;
            WordSizePixels -= oldWordSizePixels;
            WordStartBreakIndex = null;
            MaxUsedWidth = Math.Max(MaxUsedWidth, _maxSizeX);
            PosX = WordSizePixels;
        }
    }

    public int FinalizeText(out int? breakLine)
    {
        if (PosX > _maxSizeX)
        {
            if (!WordStartBreakIndex.HasValue)
            {
                Logger.Error(
                    "Assert fail inside ChatRichTextEntry.Update, " +
                    "wordStartBreakIndex is null on method end w/ word wrap required. " +
                    "Dumping relevant stuff. Send this to PJB.");
                Logger.Error($"maxSizeX: {_maxSizeX}");
                Logger.Error($"maxUsedWidth: {MaxUsedWidth}");
                Logger.Error($"breakIndexCounter: {BreakIndexCounter}");
                Logger.Error("wordStartBreakIndex: null (duh)");
                Logger.Error($"wordSizePixels: {WordSizePixels}");
                Logger.Error($"posX: {PosX}");
                Logger.Error($"lastChar: {LastRune}");

                throw new Exception(
                    "wordStartBreakIndex can only be null if the word begins at a new line," +
                    "in which case this branch shouldn't be reached as" +
                    "the word would be split due to being longer than a single line.");
            }

            breakLine = WordStartBreakIndex.Value.index;
            MaxUsedWidth = Math.Max(MaxUsedWidth, WordStartBreakIndex.Value.lineSize);
        }
        else
        {
            breakLine = null;
            MaxUsedWidth = Math.Max(MaxUsedWidth, PosX);
        }

        return (int)MaxUsedWidth;
    }

    [Pure]
    private static bool IsWordBoundary(Rune a, Rune b)
    {
        return a == new Rune(' ') || b == new Rune(' ') || a == new Rune('-') || b == new Rune('-');
    }
}
