// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared._Onyx.Paper;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Language.Paper;

[RegisterComponent]
public sealed partial class PaperLanguageComponent : Component
{
    [DataField]
    public List<LanguageTextRange> Segments = new();

    [DataField]
    public uint Revision;
}

[Serializable, NetSerializable]
public sealed class PaperLanguageViewMessage(
    string text,
    string editableText,
    uint revision,
    ulong viewGeneration,
    PaperComponent.PaperAction mode,
    List<StampDisplayInfo> stampedBy,
    List<SignatureDisplayInfo> signedBy,
    bool preserveEditor = false) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
    public readonly string EditableText = editableText;
    public readonly uint Revision = revision;
    public readonly ulong ViewGeneration = viewGeneration;
    public readonly PaperComponent.PaperAction Mode = mode;
    public readonly List<StampDisplayInfo> StampedBy = stampedBy;
    public readonly List<SignatureDisplayInfo> SignedBy = signedBy;
    public readonly bool PreserveEditor = preserveEditor;
}

[Serializable, NetSerializable]
public sealed class PaperLanguageViewPrefetchEvent(NetEntity paper, PaperLanguageViewMessage view) : EntityEventArgs
{
    public readonly NetEntity Paper = paper;
    public readonly PaperLanguageViewMessage View = view;
}

[Serializable, NetSerializable]
public sealed class PaperLanguageSaveMessage(
    uint revision,
    ulong viewGeneration,
    List<LanguageTextEdit> operations) : BoundUserInterfaceMessage
{
    public readonly uint Revision = revision;
    public readonly ulong ViewGeneration = viewGeneration;
    public readonly List<LanguageTextEdit> Operations = operations;
}

[ByRefEvent]
public record struct PaperContentChangedEvent;

[ByRefEvent]
public record struct PaperLanguageViewPrepareEvent(EntityUid Actor, bool CanWrite = false);

[ByRefEvent]
public record struct PaperLanguageExaminedEvent(ExaminedEvent Examine);

/// <summary>Paper-specific token grammar; projection and edit semantics live in LanguageDocument.</summary>
public static class PaperLanguageMarkup
{
    private static readonly Regex SignatureTag = new(
        @"\G(?:<\s*(?:sign\s*=\s*\d+|sign_(?:repeat_)?limit\s*=\s*\d+)\s*>|\[\s*(?:sign\s*=\s*\d+|sign_(?:repeat_)?limit\s*=\s*\d+)\s*\])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(20));

    private static readonly HashSet<string> AllowedTags =
    [
        "bolditalic", "bold", "bullet", "color", "head", "italic", "mono",
    ];

    public static bool IsAllowedTag(string text, int start)
    {
        if (start < 0 || start >= text.Length || text[start] != '[' || IsEscaped(text, start))
            return false;
        var end = FindTagEnd(text, start);
        return end >= 0 && FormattedMessage.TryParse(text.Substring(start, end - start + 1), out var nodes, out _) &&
               nodes.Count == 1 && IsFormattingTag(nodes[0]);
    }

    public static int GetProtectedLength(string text, int start) =>
        TryGetTagLength(text, start, out var length) ? length : 0;

    public static bool TryGetTagLength(string text, int start, out int length)
    {
        length = 0;
        if (TryGetSignatureTagLength(text, start, out length))
            return true;
        if (start < 0 || start >= text.Length || text[start] != '[' || IsEscaped(text, start))
            return false;
        var end = FindTagEnd(text, start);
        if (end < 0 || !FormattedMessage.TryParse(text.Substring(start, end - start + 1), out var nodes, out _) ||
            nodes.Count != 1 || !IsFormattingTag(nodes[0]))
            return false;
        length = end - start + 1;
        return true;
    }

    public static bool TryGetSignatureTagLength(string text, int start, out int length)
    {
        length = 0;
        if (start < 0 || start >= text.Length || text[start] is not ('<' or '[') || IsEscaped(text, start))
            return false;
        Match match;
        try
        {
            match = SignatureTag.Match(text, start);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        if (!match.Success || match.Index != start)
            return false;
        length = match.Length;
        return true;
    }

    private static bool IsEscaped(string text, int start)
    {
        var backslashes = 0;
        while (start > 0 && text[--start] == '\\')
            backslashes++;
        return backslashes % 2 != 0;
    }

    private static bool IsFormattingTag(MarkupNode node)
    {
        if (node.Name is not { } name || !AllowedTags.Contains(name) || node.Attributes.Count != 0)
            return false;
        var emptyValue = node.Value.StringValue == null && node.Value.LongValue == null && node.Value.ColorValue == null;
        if (node.Closing || name != "color")
            return emptyValue;
        return node.Value.ColorValue != null;
    }

    private static int FindTagEnd(string text, int start)
    {
        var quoted = false;
        var escaped = false;
        for (var i = start + 1; i < text.Length; i++)
        {
            var character = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if (character == '"')
                quoted = !quoted;
            if (!quoted && character == ']')
                return i;
            if (character is '[' or '\n' or '\r')
                return -1;
        }
        return -1;
    }
}
