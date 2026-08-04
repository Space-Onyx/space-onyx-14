using System.Linq;
using System.Text;
using Content.Server.Popups;
using Content.Server.Cloning;
using Content.Server.Examine;
using Content.Shared._Onyx.Language.Paper;
using Content.Shared._Onyx.Language;
using Content.Shared.Cloning.Events;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Server._Onyx.Language.Paper;

public sealed partial class PaperLanguageSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> Universal = "Universal";
    private static readonly HashSet<ProtoId<LanguagePrototype>> NonWrittenLanguages =
    [
        "Universal", "Psychomantic", "Sign", "NalRasan",
        "Cat", "Dog", "Fox", "Xeno", "Monkey", "Mouse", "Chicken", "Duck", "Cow", "Sheep",
        "Kangaroo", "Pig", "Crab", "Kobold", "Hissing", "Penguin", "Deer", "Carptongue", "Cheval",
        "FloorGoblin",
    ];
    private const float PreloadRange = 8f;
    private const float PreloadInterval = 0.25f;
    private const int MaxEditOperations = 256;
    private const int MaxLanguageSegments = 10000;

    [Dependency] private LanguageSystem _languages = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystem _examine = default!;

    private readonly Dictionary<EntityUid, int> _preserveSegments = new();
    private readonly Dictionary<(EntityUid Paper, EntityUid Actor), ViewVersion> _sentViews = new();
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<(EntityUid Paper, EntityUid Actor)> _nearbyViews = new();
    private readonly HashSet<(EntityUid Paper, EntityUid Actor)> _writers = new();
    private readonly HashSet<(EntityUid Paper, EntityUid Actor)> _pendingWriters = new();
    private readonly Dictionary<(EntityUid Paper, EntityUid Actor), ulong> _viewGenerations = new();
    private float _preloadAccumulator;
    private ulong _nextViewGeneration;

    private readonly record struct ViewVersion(uint Revision, PaperAction Mode, int Knowledge, int Stamps);

    public override void Initialize()
    {
        SubscribeLocalEvent<PaperComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<PaperComponent, PaperLanguageViewPrepareEvent>(OnPrepareView);
        SubscribeLocalEvent<PaperComponent, PaperLanguageSaveMessage>(OnSaveMessage);
        SubscribeLocalEvent<PaperComponent, PaperContentChangedEvent>(OnContentChanged);
        SubscribeLocalEvent<PaperComponent, PaperLanguageExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PaperComponent, ComponentShutdown>(OnPaperShutdown);
        SubscribeLocalEvent<PaperLanguageComponent, CloningItemEvent>(OnClonePaper, after: new[] { typeof(CloningSystem) });
    }

    private void OnExamined(Entity<PaperComponent> ent, ref PaperLanguageExaminedEvent args)
    {
        if (!args.Examine.IsInDetailsRange || ent.Comp.Content.Length == 0)
            return;

        var languages = EnsureLanguageData(ent).Segments
            .Select(segment => segment.Language)
            .Where(language => !NonWrittenLanguages.Contains(language))
            .Distinct();

        foreach (var language in languages)
        {
            var description = Loc.GetString($"paper-language-writing-{language}");
            args.Examine.PushMarkup(_languages.CanUnderstand(args.Examine.Examiner, language)
                ? Loc.GetString("paper-language-writing-known",
                    ("language", Loc.GetString($"language-{language}-name")),
                    ("description", description))
                : Loc.GetString("paper-language-writing-unknown", ("description", description)));
        }
    }

    private void OnPaperShutdown(Entity<PaperComponent> ent, ref ComponentShutdown args)
    {
        _preserveSegments.Remove(ent.Owner);
        _writers.RemoveWhere(key => key.Paper == ent.Owner);
        _pendingWriters.RemoveWhere(key => key.Paper == ent.Owner);
        foreach (var key in _viewGenerations.Keys.Where(key => key.Paper == ent.Owner).ToList())
            _viewGenerations.Remove(key);
        foreach (var key in _sentViews.Keys.Where(key => key.Paper == ent.Owner).ToList())
            _sentViews.Remove(key);
    }

    public override void Update(float frameTime)
    {
        _preloadAccumulator += frameTime;
        if (_preloadAccumulator < PreloadInterval)
            return;

        _preloadAccumulator = 0f;
        _nearbyViews.Clear();
        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var actor, out _))
        {
            _nearby.Clear();
            _lookup.GetEntitiesInRange(actor, PreloadRange, _nearby);
            foreach (var paper in _nearby)
            {
                if (TryComp<PaperComponent>(paper, out var component))
                {
                    if (_ui.IsUiOpen(paper, PaperUiKey.Key, actor))
                        continue;
                    if (!_examine.InRangeUnOccluded(actor, paper, PreloadRange))
                        continue;
                    _nearbyViews.Add((paper, actor));
                    SendPrefetchedView((paper, component), actor);
                }
            }
        }

        foreach (var key in _sentViews.Keys.Where(key => !_nearbyViews.Contains(key)).ToList())
            _sentViews.Remove(key);
        _pendingWriters.RemoveWhere(key => !_ui.IsUiOpen(key.Paper, PaperUiKey.Key, key.Actor));
        _writers.RemoveWhere(key => !Exists(key.Actor) || !HasComp<ActorComponent>(key.Actor));
        foreach (var key in _viewGenerations.Keys.Where(key => !Exists(key.Actor) || !HasComp<ActorComponent>(key.Actor)).ToList())
            _viewGenerations.Remove(key);
    }

    private void OnUiOpened(Entity<PaperComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(PaperUiKey.Key))
        {
            if (_pendingWriters.Remove((ent.Owner, args.Actor)))
                _writers.Add((ent.Owner, args.Actor));
            SendView(ent, args.Actor);
        }
    }

    private void OnUiClosed(Entity<PaperComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(PaperUiKey.Key))
        {
            _writers.Remove((ent.Owner, args.Actor));
            _pendingWriters.Remove((ent.Owner, args.Actor));
        }
    }

    private void OnPrepareView(Entity<PaperComponent> ent, ref PaperLanguageViewPrepareEvent args)
    {
        if (args.CanWrite)
        {
            if (_ui.IsUiOpen(ent.Owner, PaperUiKey.Key, args.Actor))
                _writers.Add((ent.Owner, args.Actor));
            else
                _pendingWriters.Add((ent.Owner, args.Actor));
        }
        PrefetchView(ent, args.Actor);
    }

    private void PrefetchView(Entity<PaperComponent> ent, EntityUid actor)
    {
        if (_ui.IsUiOpen(ent.Owner, PaperUiKey.Key, actor))
        {
            SendView(ent, actor);
            return;
        }

        SendPrefetchedView(ent, actor);
    }

    private void SendPrefetchedView(Entity<PaperComponent> ent, EntityUid actor)
    {
        var key = (ent.Owner, actor);
        var version = GetViewVersion(ent, actor);
        if (_sentViews.TryGetValue(key, out var sent) && sent == version)
            return;

        _sentViews[key] = version;
        RaiseNetworkEvent(new PaperLanguageViewPrefetchEvent(GetNetEntity(ent.Owner), BuildViewMessage(ent, actor)), actor);
    }

    private ViewVersion GetViewVersion(Entity<PaperComponent> ent, EntityUid actor)
    {
        var revision = EnsureLanguageData(ent).Revision;
        var knowledge = 0;
        if (TryComp<LanguageSpeakerComponent>(actor, out var speaker))
        {
            knowledge = speaker.UnderstandsAllLanguages ? 1 : 0;
            foreach (var language in speaker.UnderstoodLanguages.OrderBy(id => id.Id, StringComparer.Ordinal))
                knowledge = HashCode.Combine(knowledge, language.Id.GetHashCode(StringComparison.Ordinal));
        }

        var stamps = 0;
        foreach (var stamp in ent.Comp.StampedBy)
            stamps = HashCode.Combine(stamps, stamp.GetHashCode());

        var mode = _writers.Contains((ent.Owner, actor)) || _pendingWriters.Contains((ent.Owner, actor))
            ? PaperAction.Write
            : PaperAction.Read;
        return new ViewVersion(revision, mode, knowledge, stamps);
    }

    private void OnSaveMessage(Entity<PaperComponent> ent, ref PaperLanguageSaveMessage args)
    {
        if (!_writers.Contains((ent.Owner, args.Actor)) || ent.Comp.EditingDisabled ||
            !_ui.IsUiOpen(ent.Owner, PaperUiKey.Key, args.Actor))
        {
            SendView(ent, args.Actor);
            return;
        }

        var writeAttempt = new PaperWriteAttemptEvent(ent.Owner);
        RaiseLocalEvent(args.Actor, ref writeAttempt);
        if (writeAttempt.Cancelled)
        {
            SendView(ent, args.Actor, true);
            return;
        }

        var data = EnsureLanguageData(ent);
        if (args.Revision != data.Revision ||
            !_viewGenerations.TryGetValue((ent.Owner, args.Actor), out var generation) ||
            args.ViewGeneration != generation)
        {
            SendView(ent, args.Actor);
            return;
        }

        var insertsText = args.Operations?.Any(operation => !string.IsNullOrEmpty(operation.InsertedText)) == true;
        var language = _languages.GetCurrentLanguage(args.Actor);
        if (insertsText && !_languages.CanSpeak(args.Actor, language.ID))
        {
            SendView(ent, args.Actor, true);
            return;
        }

        if (language.RequiresSight && insertsText)
        {
            _popup.PopupEntity(Loc.GetString("paper-language-cannot-write"), ent, args.Actor);
            SendView(ent, args.Actor, true);
            return;
        }

        if (args.Operations == null || args.Operations.Count > MaxEditOperations)
        {
            SendView(ent, args.Actor, true);
            return;
        }

        if (args.Operations.Count == 0)
        {
            _writers.Remove((ent.Owner, args.Actor));
            SendView(ent, args.Actor);
            return;
        }

        var spans = BuildEditSpans(ent.Comp.Content, data.Segments, args.Actor);
        var currentLength = spans.Sum(span => span.ViewText.Length);
        var insertedBudget = 0;
        foreach (var operation in args.Operations)
        {
            if (operation.InsertedText == null || operation.InsertedText.Length > ent.Comp.ContentSize ||
                operation.Start < 0 || operation.DeleteLength < 0 || operation.Start > currentLength ||
                operation.DeleteLength > currentLength - operation.Start ||
                operation.InsertedText.Length > ent.Comp.ContentSize - insertedBudget ||
                !IsWellFormedUtf16(operation.InsertedText) || !IsScalarBoundary(spans, operation.Start) ||
                !IsScalarBoundary(spans, operation.Start + operation.DeleteLength))
            {
                SendView(ent, args.Actor, true);
                return;
            }
            if (!PaperLanguageEditReplay.Apply(spans, operation, language.ID))
            {
                _popup.PopupEntity(Loc.GetString("paper-language-cannot-edit-unknown"), ent, args.Actor);
                SendView(ent, args.Actor);
                return;
            }
            insertedBudget += operation.InsertedText.Length;
            currentLength = currentLength - operation.DeleteLength + operation.InsertedText.Length;
        }

        PaperLanguageEditReplay.CanonicalizeSeparators(spans);

        var content = new StringBuilder();
        var segments = new List<PaperLanguageSegment>();
        foreach (var span in spans)
        {
            content.Append(span.SourceText);
            AddSegment(segments, content.Length - span.SourceText.Length, span.SourceText.Length, span.Language);
        }

        if (content.Length > ent.Comp.ContentSize || !SegmentsCover(segments, content.Length))
        {
            SendView(ent, args.Actor, true);
            return;
        }

        data.Segments = segments;
        data.Revision++;
        _writers.Remove((ent.Owner, args.Actor));
        PreserveNextContentChange(ent.Owner);
        _paper.ApplyWrittenContent(ent, args.Actor, content.ToString());
    }

    private void OnContentChanged(Entity<PaperComponent> ent, ref PaperContentChangedEvent args)
    {
        var data = EnsureLanguageData(ent);
        if (!_preserveSegments.TryGetValue(ent.Owner, out var preserve) || preserve <= 0)
        {
            data.Segments = PaperLanguageSegments.ForText(ent.Comp.Content, Universal);
            data.Revision++;
        }
        else if (preserve == 1)
            _preserveSegments.Remove(ent.Owner);
        else
            _preserveSegments[ent.Owner] = preserve - 1;

        Dirty(ent.Owner, data);
        foreach (var actor in _ui.GetActors(ent.Owner, PaperUiKey.Key).ToList())
            SendView(ent, actor);
    }

    public void SetContent(
        Entity<PaperComponent> ent,
        string content,
        IEnumerable<PaperLanguageSegment> segments)
    {
        var data = EnsureLanguageData(ent);
        data.Segments = segments.Take(MaxLanguageSegments)
            .Select(segment => new PaperLanguageSegment(
                segment.Start,
                segment.Length,
                _prototypes.HasIndex(segment.Language) ? segment.Language : Universal))
            .ToList();
        PaperLanguageSegments.Normalize(data.Segments, content.Length);
        data.Revision++;
        PreserveNextContentChange(ent.Owner);
        _paper.SetContent(ent, content);
    }

    private void PreserveNextContentChange(EntityUid paper)
    {
        _preserveSegments[paper] = _preserveSegments.GetValueOrDefault(paper) + 1;
    }

    private void OnClonePaper(Entity<PaperLanguageComponent> ent, ref CloningItemEvent args)
    {
        if (!TryComp<PaperComponent>(ent.Owner, out var sourcePaper) ||
            !TryComp<PaperComponent>(args.CloneUid, out var clonePaper))
            return;

        SetContent((args.CloneUid, clonePaper), sourcePaper.Content, ent.Comp.Segments);
    }

    public List<PaperLanguageSegment> CopySegments(EntityUid paper)
    {
        return TryComp<PaperLanguageComponent>(paper, out var data)
            ? PaperLanguageSegments.Clone(data.Segments)
            : new List<PaperLanguageSegment>();
    }

    private PaperLanguageComponent EnsureLanguageData(Entity<PaperComponent> ent)
    {
        var data = EnsureComp<PaperLanguageComponent>(ent);
        if (!SegmentsCover(data.Segments, ent.Comp.Content.Length))
        {
            data.Segments = PaperLanguageSegments.ForText(ent.Comp.Content, Universal);
            data.Revision++;
            Dirty(ent.Owner, data);
            foreach (var key in _sentViews.Keys.Where(key => key.Paper == ent.Owner).ToList())
                _sentViews.Remove(key);
        }
        return data;
    }

    private void SendView(Entity<PaperComponent> ent, EntityUid actor, bool preserveEditor = false)
    {
        _ui.ServerSendUiMessage(ent.Owner, PaperUiKey.Key, BuildViewMessage(ent, actor, preserveEditor), actor);
    }

    private PaperLanguageViewMessage BuildViewMessage(
        Entity<PaperComponent> ent,
        EntityUid actor,
        bool preserveEditor = false)
    {
        var data = EnsureLanguageData(ent);
        var (editable, rendered) = BuildViews(ent.Comp.Content, data.Segments, actor);
        var generation = ++_nextViewGeneration;
        _viewGenerations[(ent.Owner, actor)] = generation;
        var mode = _writers.Contains((ent.Owner, actor)) || _pendingWriters.Contains((ent.Owner, actor))
            ? PaperAction.Write
            : PaperAction.Read;
        return new PaperLanguageViewMessage(rendered, editable, data.Revision, generation, mode,
            new List<StampDisplayInfo>(ent.Comp.StampedBy), preserveEditor);
    }

    private (string Editable, string Rendered) BuildViews(
        string content,
        List<PaperLanguageSegment> segments,
        EntityUid reader)
    {
        var editable = new StringBuilder(content.Length);
        var rendered = new StringBuilder(content.Length);
        foreach (var span in BuildEditSpans(content, segments, reader))
        {
            if (!_prototypes.TryIndex(span.Language, out LanguagePrototype? language))
                continue;

            editable.Append(span.ViewText);
            rendered.Append($"[paperlang=\"{FormattedMessage.EscapeStringParameter(language.ID)}\"]");
            rendered.Append(SanitizeLanguageTags(span.ViewText));
            rendered.Append("[/paperlang]");
        }
        return (editable.ToString(), rendered.ToString());
    }

    private List<PaperLanguageEditSpan> BuildEditSpans(
        string content,
        List<PaperLanguageSegment> segments,
        EntityUid reader)
    {
        var spans = new List<PaperLanguageEditSpan>();
        foreach (var segment in segments)
        {
            var effectiveLanguage = segment.Language;
            if (!_prototypes.TryIndex(effectiveLanguage, out LanguagePrototype? language))
            {
                effectiveLanguage = Universal;
                language = _prototypes.Index(Universal);
            }

            var understood = _languages.CanUnderstand(reader, effectiveLanguage);
            var end = segment.Start + segment.Length;
            var start = segment.Start;
            while (start < end)
            {
                var newline = content.IndexOf('\n', start, end - start);
                var bodyEnd = newline < 0 ? end : newline;
                var source = content.Substring(start, bodyEnd - start);
                var view = source;
                if (!understood)
                {
                    view = ObfuscateMarkupLine(view, language);
                    if (source.Length > 0 && view.Length == 0)
                        view = "<?>";
                }
                spans.Add(new PaperLanguageEditSpan(view, source, effectiveLanguage, understood && view.Length == source.Length));

                if (newline >= 0)
                    spans.Add(new PaperLanguageEditSpan("\n", "\n", effectiveLanguage, true));
                start = newline < 0 ? end : newline + 1;
            }
        }
        return spans;
    }


    private string ObfuscateMarkupLine(string value, LanguagePrototype language)
    {
        var protectedText = new StringBuilder(value.Length);
        var tags = new List<string>();
        var escaped = false;
        var inTag = false;
        var tag = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (escaped)
            {
                protectedText.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                protectedText.Append(character);
                escaped = true;
                continue;
            }

            if (!inTag && character == '[' &&
                PaperLanguageMarkup.IsAllowedTag(value, i))
            {
                inTag = true;
                tag.Append(character);
                continue;
            }

            if (inTag)
                tag.Append(character);
            else
                protectedText.Append(character);

            if (inTag && character == ']')
            {
                inTag = false;
                tags.Add(tag.ToString());
                tag.Clear();
                protectedText.Append('\uE000');
            }
        }

        if (tag.Length > 0)
            protectedText.Append(tag);

        var output = new StringBuilder(_languages.Obfuscate(protectedText.ToString(), language));
        if (tags.Count > 0 && !output.ToString().Contains('\uE000'))
        {
            var opening = tags.Where(markup => !markup.StartsWith("[/", StringComparison.Ordinal));
            var closing = tags.Where(markup => markup.StartsWith("[/", StringComparison.Ordinal));
            return string.Concat(opening) + output + string.Concat(closing);
        }

        foreach (var markup in tags)
        {
            var index = output.ToString().IndexOf('\uE000');
            if (index < 0)
                break;
            output.Remove(index, 1);
            output.Insert(index, markup);
        }
        return output.ToString();
    }

    private static string SanitizeLanguageTags(string text)
    {
        return text
            .Replace("[paperlang", "［paperlang", StringComparison.OrdinalIgnoreCase)
            .Replace("[/paperlang", "［/paperlang", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SegmentsCover(List<PaperLanguageSegment> segments, int length)
    {
        if (length == 0)
            return segments.Count == 0;
        var position = 0;
        foreach (var segment in segments)
        {
            if (segment.Start != position || segment.Length <= 0 || segment.Length > length - position)
                return false;
            position += segment.Length;
        }
        return position == length;
    }

    private static void AddSegment(
        List<PaperLanguageSegment> segments,
        int start,
        int length,
        ProtoId<LanguagePrototype> language)
    {
        if (length == 0)
            return;
        if (segments.Count > 0 && segments[^1].Language == language &&
            segments[^1].Start + segments[^1].Length == start)
        {
            segments[^1].Length += length;
            return;
        }
        segments.Add(new PaperLanguageSegment(start, length, language));
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (++i >= value.Length || !char.IsLowSurrogate(value[i]))
                    return false;
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsScalarBoundary(List<PaperLanguageEditSpan> spans, int index)
    {
        if (index <= 0)
            return index == 0;

        var position = 0;
        char? previous = null;
        foreach (var span in spans)
        {
            foreach (var character in span.ViewText)
            {
                if (position == index)
                    return previous is not { } before || !char.IsHighSurrogate(before) || !char.IsLowSurrogate(character);
                previous = character;
                position++;
            }
        }
        return index == position;
    }

}
