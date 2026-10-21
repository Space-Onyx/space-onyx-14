// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Language;

/// <summary>A source range in any multilingual document. Offsets use UTF-16, like TextEdit.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class LanguageTextRange
{
    [DataField]
    public int Start;

    [DataField]
    public int Length;

    [DataField]
    public ProtoId<LanguagePrototype> Language = "Universal";

    public LanguageTextRange() { }

    public LanguageTextRange(int start, int length, ProtoId<LanguagePrototype> language)
    {
        Start = start;
        Length = length;
        Language = language;
    }
}

/// <summary>Server-only projection mapping. SourceText must never be sent to an unknown reader.</summary>
public sealed record LanguageTextSpan(string ViewText, string SourceText, ProtoId<LanguagePrototype> Language, bool Exact);

[Serializable, NetSerializable]
public readonly record struct LanguageTextEdit(
    int Start,
    int DeleteLength,
    string InsertedText,
    ProtoId<LanguagePrototype>? Language = null);

/// <summary>Consumer-independent projection and transactional editing of multilingual text.</summary>
public static class LanguageDocument
{
    public static List<LanguageTextRange> ForText(string text, ProtoId<LanguagePrototype> language) =>
        text.Length == 0 ? new() : new() { new(0, text.Length, language) };

    public static List<LanguageTextRange> Clone(IEnumerable<LanguageTextRange> ranges)
    {
        var result = new List<LanguageTextRange>();
        foreach (var range in ranges)
            result.Add(new LanguageTextRange(range.Start, range.Length, range.Language));
        return result;
    }

    /// <summary>Repairs persisted ranges without dropping valid text; uncovered text is Universal.</summary>
    public static void Normalize(List<LanguageTextRange> ranges, int length)
    {
        length = Math.Max(0, length);
        var ordered = ranges.OrderBy(range => range.Start);
        var normalized = new List<LanguageTextRange>();
        var position = 0;
        foreach (var range in ordered)
        {
            if (range.Start < 0 || range.Start >= length || range.Length <= 0)
                continue;
            if (range.Start > position)
                AppendRange(normalized, position, range.Start - position, "Universal");
            var start = Math.Max(position, range.Start);
            var end = range.Start + Math.Min(range.Length, length - range.Start);
            if (end <= start)
                continue;
            AppendRange(normalized, start, end - start, range.Language);
            position = end;
        }
        if (position < length)
            AppendRange(normalized, position, length - position, "Universal");
        ranges.Clear();
        ranges.AddRange(normalized);
    }

    public static bool Covers(IReadOnlyList<LanguageTextRange> ranges, int length)
    {
        var position = 0;
        foreach (var range in ranges)
        {
            if (range.Start != position || range.Length <= 0 || range.Length > length - position)
                return false;
            position += range.Length;
        }
        return position == length;
    }

    public static void AppendRange(List<LanguageTextRange> ranges, int start, int length, ProtoId<LanguagePrototype> language)
    {
        if (length <= 0)
            return;
        if (ranges.Count > 0 && ranges[^1].Language == language && ranges[^1].Start + ranges[^1].Length == start)
            ranges[^1].Length += length;
        else
            ranges.Add(new LanguageTextRange(start, length, language));
    }

    /// <summary>Opaque tokens are kept outside obfuscation, even across source-language boundaries.</summary>
    public static List<LanguageTextSpan> Project(
        string text,
        IReadOnlyList<LanguageTextRange> ranges,
        Func<ProtoId<LanguagePrototype>, bool> understands,
        Func<string, ProtoId<LanguagePrototype>, string> obfuscate,
        Func<string, int, int>? protectedLength = null)
    {
        if (!Covers(ranges, text.Length) || !IsWellFormed(text))
            throw new ArgumentException("Language ranges must cover the document.", nameof(ranges));

        var spans = new List<LanguageTextSpan>();
        var rangeIndex = 0;
        for (var position = 0; position < text.Length;)
        {
            while (position >= ranges[rangeIndex].Start + ranges[rangeIndex].Length)
                rangeIndex++;
            var range = ranges[rangeIndex];
            var tokenLength = protectedLength?.Invoke(text, position) ?? 0;
            if (tokenLength < 0)
                throw new ArgumentOutOfRangeException(nameof(protectedLength));
            if (tokenLength > 0)
            {
                if (tokenLength > text.Length - position)
                    throw new ArgumentOutOfRangeException(nameof(protectedLength));
                var token = text.Substring(position, tokenLength);
                spans.Add(new LanguageTextSpan(token, token, "Universal", true));
                position += tokenLength;
                continue;
            }

            var start = position++;
            var end = range.Start + range.Length;
            // A persisted range may end between surrogate halves. Keep the scalar together.
            if (end < text.Length && end > 0 && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
                end++;
            while (text[start] != '\n' && position < end && text[position] != '\n' &&
                   (protectedLength?.Invoke(text, position) ?? 0) <= 0)
                position++;
            // Preserve line breaks without allowing replacement obfuscators to erase layout.
            if (text[start] == '\n')
                position = start + 1;
            var source = text[start..position];
            var exact = source == "\n" || understands(range.Language);
            var view = exact ? source : obfuscate(source, range.Language);
            if (!IsWellFormed(view))
                throw new ArgumentException("Text projection must contain well-formed UTF-16.", nameof(obfuscate));
            if (!exact && view.Length == 0)
                view = "<?>";
            spans.Add(new LanguageTextSpan(view, source, range.Language, exact));
        }
        return spans;
    }

    /// <summary>Edits may delete an opaque span, but never split it or substitute its hidden source.</summary>
    public static bool TryEdit(List<LanguageTextSpan> spans, LanguageTextEdit edit, ProtoId<LanguagePrototype> language)
    {
        if (edit.Start < 0 || edit.DeleteLength < 0 || edit.InsertedText == null || !IsWellFormed(edit.InsertedText))
            return false;
        var viewLength = 0;
        char? beforeStart = null;
        char? atStart = null;
        char? beforeEnd = null;
        char? atEnd = null;
        var requestedEnd = (long) edit.Start + edit.DeleteLength;
        foreach (var span in spans)
        {
            if (span.ViewText.Length > int.MaxValue - viewLength ||
                span.Exact && span.ViewText != span.SourceText)
                return false;
            Capture(edit.Start, ref beforeStart, ref atStart);
            if (requestedEnd <= int.MaxValue)
                Capture((int) requestedEnd, ref beforeEnd, ref atEnd);
            viewLength += span.ViewText.Length;

            void Capture(int index, ref char? before, ref char? at)
            {
                var local = index - viewLength;
                if (local > 0 && local <= span.ViewText.Length)
                    before = span.ViewText[local - 1];
                if (local >= 0 && local < span.ViewText.Length)
                    at = span.ViewText[local];
            }
        }
        if (edit.Start > viewLength || edit.DeleteLength > viewLength - edit.Start)
            return false;
        var end = edit.Start + edit.DeleteLength;
        if (SplitsScalar(beforeStart, atStart) || SplitsScalar(beforeEnd, atEnd))
            return false;

        var result = new List<LanguageTextSpan>(spans.Count + 2);
        var offset = 0;
        var inserted = false;
        foreach (var span in spans)
        {
            var next = offset + span.ViewText.Length;
            if (!span.Exact && (edit.Start > offset && edit.Start < next || end > offset && end < next))
                return false;

            if (next <= edit.Start)
                result.Add(span);
            else if (offset >= end)
            {
                Insert();
                result.Add(span);
            }
            else
            {
                var left = Math.Max(0, edit.Start - offset);
                var right = Math.Min(span.ViewText.Length, end - offset);
                if (left > 0)
                    result.Add(new LanguageTextSpan(span.ViewText[..left], span.SourceText[..left], span.Language, true));
                Insert();
                if (right < span.ViewText.Length)
                    result.Add(new LanguageTextSpan(span.ViewText[right..], span.SourceText[right..], span.Language, true));
            }
            offset = next;
        }
        Insert();
        spans.Clear();
        spans.AddRange(result);
        return true;

        void Insert()
        {
            if (inserted)
                return;
            inserted = true;
            if (edit.InsertedText.Length > 0)
                result.Add(new LanguageTextSpan(edit.InsertedText, edit.InsertedText, language, true));
        }
    }

    private static bool SplitsScalar(char? before, char? at) =>
        before is { } high && at is { } low && char.IsHighSurrogate(high) && char.IsLowSurrogate(low);

    public static bool IsWellFormed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i >= text.Length || !char.IsLowSurrogate(text[i]))
                    return false;
            }
            else if (char.IsLowSurrogate(text[i]))
                return false;
        }
        return true;
    }
}
