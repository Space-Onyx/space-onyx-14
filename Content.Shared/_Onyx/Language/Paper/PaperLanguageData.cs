using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared._Onyx.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Language.Paper;

[RegisterComponent]
public sealed partial class PaperLanguageComponent : Component
{
    [DataField]
    public List<PaperLanguageSegment> Segments = new();

    [DataField]
    public uint Revision;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class PaperLanguageSegment
{
    [DataField]
    public int Start;

    [DataField]
    public int Length;

    [DataField]
    public ProtoId<LanguagePrototype> Language = "Universal";

    public PaperLanguageSegment()
    {
    }

    public PaperLanguageSegment(int start, int length, ProtoId<LanguagePrototype> language)
    {
        Start = start;
        Length = length;
        Language = language;
    }
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
public sealed class PaperLanguageViewPrefetchEvent(
    NetEntity paper,
    PaperLanguageViewMessage view) : EntityEventArgs
{
    public readonly NetEntity Paper = paper;
    public readonly PaperLanguageViewMessage View = view;
}

[Serializable, NetSerializable]
public sealed class PaperLanguageSaveMessage(
    uint revision,
    ulong viewGeneration,
    List<PaperLanguageEditOperation> operations) : BoundUserInterfaceMessage
{
    public readonly uint Revision = revision;
    public readonly ulong ViewGeneration = viewGeneration;
    public readonly List<PaperLanguageEditOperation> Operations = operations;
}

[Serializable, NetSerializable]
public readonly record struct PaperLanguageEditOperation(
    int Start,
    int DeleteLength,
    string InsertedText);

public sealed record PaperLanguageEditSpan(
    string ViewText,
    string SourceText,
    ProtoId<LanguagePrototype> Language,
    bool Exact);

public static class PaperLanguageEditReplay
{
    public static bool Apply(
        List<PaperLanguageEditSpan> spans,
        PaperLanguageEditOperation operation,
        ProtoId<LanguagePrototype> language)
    {
        var viewLength = spans.Sum(span => span.ViewText.Length);
        if (operation.Start < 0 || operation.DeleteLength < 0 ||
            operation.Start > viewLength || operation.DeleteLength > viewLength - operation.Start ||
            operation.InsertedText == null)
            return false;

        var end = operation.Start + operation.DeleteLength;
        var output = new List<PaperLanguageEditSpan>(spans.Count + 2);
        var position = 0;
        var inserted = false;
        foreach (var span in spans)
        {
            var spanStart = position;
            var spanEnd = position + span.ViewText.Length;
            position = spanEnd;

            if (operation.DeleteLength == 0 && !inserted && operation.Start >= spanStart && operation.Start <= spanEnd)
            {
                var offset = operation.Start - spanStart;
                if (!span.Exact && offset > 0 && offset < span.ViewText.Length)
                    return false;
                if (!span.Exact && offset == span.ViewText.Length)
                {
                    output.Add(span);
                    AddInserted(output, operation.InsertedText, language);
                    inserted = true;
                    continue;
                }
                if (span.Exact && offset > 0)
                    output.Add(new PaperLanguageEditSpan(span.ViewText[..offset], span.SourceText[..offset], span.Language, true));
                AddInserted(output, operation.InsertedText, language);
                inserted = true;
                if (span.Exact && offset > 0)
                {
                    output.Add(new PaperLanguageEditSpan(span.ViewText[offset..], span.SourceText[offset..], span.Language, true));
                    continue;
                }
            }

            if (spanEnd <= operation.Start || spanStart >= end)
            {
                output.Add(span);
                continue;
            }

            var overlapStart = Math.Max(operation.Start, spanStart) - spanStart;
            var overlapEnd = Math.Min(end, spanEnd) - spanStart;
            var full = overlapStart == 0 && overlapEnd == span.ViewText.Length;
            if (!span.Exact && !full)
                return false;

            if (span.Exact && overlapStart > 0)
                output.Add(new PaperLanguageEditSpan(span.ViewText[..overlapStart], span.SourceText[..overlapStart], span.Language, true));
            if (!inserted)
            {
                AddInserted(output, operation.InsertedText, language);
                inserted = true;
            }
            if (span.Exact && overlapEnd < span.ViewText.Length)
                output.Add(new PaperLanguageEditSpan(span.ViewText[overlapEnd..], span.SourceText[overlapEnd..], span.Language, true));
        }

        if (!inserted)
            AddInserted(output, operation.InsertedText, language);

        spans.Clear();
        spans.AddRange(output.Where(span => span.SourceText.Length > 0));
        return true;
    }

    public static void CanonicalizeSeparators(List<PaperLanguageEditSpan> spans)
    {
        for (var i = 0; i < spans.Count;)
        {
            if (!IsSeparator(spans[i].SourceText))
            {
                i++;
                continue;
            }

            var end = i + 1;
            while (end < spans.Count && IsSeparator(spans[end].SourceText))
                end++;
            var language = end < spans.Count ? spans[end].Language : i > 0 ? spans[i - 1].Language : spans[i].Language;
            for (var j = i; j < end; j++)
                spans[j] = spans[j] with { Language = language };
            i = end;
        }

        static bool IsSeparator(string text) => text.All(character => !char.IsLetterOrDigit(character));
    }

    private static void AddInserted(
        List<PaperLanguageEditSpan> spans,
        string text,
        ProtoId<LanguagePrototype> language)
    {
        if (text.Length > 0)
            spans.Add(new PaperLanguageEditSpan(text, text, language, true));
    }
}

[ByRefEvent]
public record struct PaperContentChangedEvent;

[ByRefEvent]
public record struct PaperLanguageViewPrepareEvent(EntityUid Actor, bool CanWrite = false);

[ByRefEvent]
public record struct PaperLanguageExaminedEvent(ExaminedEvent Examine);

public static class PaperLanguageSegments
{
    public static List<PaperLanguageSegment> ForText(string text, ProtoId<LanguagePrototype> language)
    {
        return text.Length == 0
            ? new List<PaperLanguageSegment>()
            : new List<PaperLanguageSegment> { new(0, text.Length, language) };
    }

    public static List<PaperLanguageSegment> Clone(IEnumerable<PaperLanguageSegment> segments)
    {
        return segments.Select(segment => new PaperLanguageSegment(segment.Start, segment.Length, segment.Language)).ToList();
    }

    public static void Normalize(List<PaperLanguageSegment> segments, int contentLength)
    {
        contentLength = Math.Max(0, contentLength);
        segments.RemoveAll(segment => segment.Length <= 0 || segment.Start < 0 || segment.Start >= contentLength);
        var ordered = segments.Select((segment, index) => (segment, index))
            .OrderBy(entry => entry.segment.Start)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.segment)
            .ToList();

        var normalized = new List<PaperLanguageSegment>();
        var position = 0;
        foreach (var segment in ordered)
        {
            if (segment.Start > position)
                Add(normalized, position, segment.Start - position, "Universal");

            var start = Math.Max(position, segment.Start);
            var end = segment.Start + Math.Min(segment.Length, contentLength - segment.Start);
            if (start >= end)
                continue;

            Add(normalized, start, end - start, segment.Language);
            position = end;
        }

        if (position < contentLength)
            Add(normalized, position, contentLength - position, "Universal");

        segments.Clear();
        segments.AddRange(normalized);

        static void Add(
            List<PaperLanguageSegment> target,
            int start,
            int length,
            ProtoId<LanguagePrototype> language)
        {
            if (length <= 0)
                return;
            if (target.Count > 0 && target[^1].Language == language &&
                target[^1].Length <= start && target[^1].Start == start - target[^1].Length)
            {
                target[^1].Length += length;
                return;
            }
            target.Add(new PaperLanguageSegment(start, length, language));
        }
    }

    public static void MakeMarkupUniversal(string text, List<PaperLanguageSegment> segments)
    {
        for (var start = 0; start < text.Length; start++)
        {
            if (!PaperLanguageMarkup.TryGetTagLength(text, start, out var length))
                continue;

            SetLanguage(segments, start, length, "Universal");
            start += length - 1;
        }

        Normalize(segments, text.Length);
    }

    private static void SetLanguage(
        List<PaperLanguageSegment> segments,
        int start,
        int length,
        ProtoId<LanguagePrototype> language)
    {
        var end = start + length;
        var output = new List<PaperLanguageSegment>(segments.Count + 2);
        foreach (var segment in segments)
        {
            var segmentEnd = segment.Start + segment.Length;
            if (segmentEnd <= start || segment.Start >= end)
            {
                output.Add(new PaperLanguageSegment(segment.Start, segment.Length, segment.Language));
                continue;
            }

            if (segment.Start < start)
                output.Add(new PaperLanguageSegment(segment.Start, start - segment.Start, segment.Language));
            output.Add(new PaperLanguageSegment(Math.Max(segment.Start, start),
                Math.Min(segmentEnd, end) - Math.Max(segment.Start, start), language));
            if (segmentEnd > end)
                output.Add(new PaperLanguageSegment(end, segmentEnd - end, segment.Language));
        }

        segments.Clear();
        segments.AddRange(output);
    }

}

public static class PaperLanguageMarkup
{
    private static readonly Regex SignatureTag = new(
        @"(?:<\s*(?:sign\s*=\s*\d+|sign_(?:repeat_)?limit\s*=\s*\d+)\s*>|\[\s*(?:sign\s*=\s*\d+|sign_(?:repeat_)?limit\s*=\s*\d+)\s*\])",
        RegexOptions.IgnoreCase);

    private static readonly HashSet<string> AllowedTags =
    [
        "bolditalic",
        "bold",
        "bullet",
        "color",
        "head",
        "italic",
        "mono",
    ];

    public static bool IsAllowedTag(string text, int start)
    {
        if (start < 0 || start >= text.Length || text[start] != '[')
            return false;

        var end = text.IndexOf(']', start + 1);
        if (end < 0)
            return false;

        var candidate = text.Substring(start, end - start + 1);
        if (!FormattedMessage.TryParse(candidate, out var nodes, out _) ||
            nodes.Count != 1 ||
            nodes[0].Name is not { } name ||
            !AllowedTags.Contains(name))
            return false;

        return true;
    }

    public static bool TryGetTagLength(string text, int start, out int length)
    {
        length = 0;
        if (TryGetSignatureTagLength(text, start, out length))
            return true;
        if (!IsAllowedTag(text, start))
            return false;

        length = text.IndexOf(']', start + 1) - start + 1;
        return true;
    }

    public static bool TryGetSignatureTagLength(string text, int start, out int length)
    {
        length = 0;
        if (start < 0 || start >= text.Length || text[start] is not ('<' or '['))
            return false;

        var match = SignatureTag.Match(text, start);
        if (!match.Success || match.Index != start)
            return false;

        length = match.Length;
        return true;
    }
}
