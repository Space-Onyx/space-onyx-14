using System.Linq;
using System.Text;
using Content.Server.Popups;
using Content.Server.Cloning;
using Content.Shared._Onyx.Language.Paper;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Paper;
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
    private const int MaxEditOperations = 256;
    private const int MaxLanguageSegments = 10000;

    [Dependency] private LanguageSystem _languages = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntityUid, int> _preserveSegments = new();
    private readonly HashSet<(EntityUid Paper, EntityUid Actor)> _writers = new();
    private readonly HashSet<(EntityUid Paper, EntityUid Actor)> _pendingWriters = new();
    private readonly Dictionary<(EntityUid Paper, EntityUid Actor), ulong> _viewGenerations = new();
    private ulong _nextViewGeneration;

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
        SubscribeLocalEvent<LanguageSpeakerComponent, LanguageStateChangedEvent>(OnLanguageStateChanged);
    }

    private void OnExamined(Entity<PaperComponent> ent, ref PaperLanguageExaminedEvent args)
    {
        if (!args.Examine.IsInDetailsRange || ent.Comp.Content.Length == 0)
            return;

        var languages = EnsureLanguageData(ent).Segments
            .Select(segment => segment.Language)
            .Where(id => _prototypes.TryIndex(id, out var language) && language.Writing.Description != null)
            .Distinct();

        foreach (var language in languages)
        {
            var description = Loc.GetString(_prototypes.Index(language).Writing.Description!.Value);
            args.Examine.PushMarkup(_languages.CanRead(args.Examine.Examiner, language)
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
        foreach (var key in _viewGenerations.Keys.ToArray())
        {
            if (key.Paper == ent.Owner)
                _viewGenerations.Remove(key);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _pendingWriters.RemoveWhere(key => !_ui.IsUiOpen(key.Paper, PaperUiKey.Key, key.Actor));
        _writers.RemoveWhere(key => !Exists(key.Actor) || !HasComp<ActorComponent>(key.Actor));
        foreach (var key in _viewGenerations.Keys.ToArray())
        {
            if (!Exists(key.Actor) || !HasComp<ActorComponent>(key.Actor))
                _viewGenerations.Remove(key);
        }
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
            _viewGenerations.Remove((ent.Owner, args.Actor));
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
        RaiseNetworkEvent(new PaperLanguageViewPrefetchEvent(GetNetEntity(ent.Owner), BuildViewMessage(ent, actor)), actor);
    }

    private void OnLanguageStateChanged(Entity<LanguageSpeakerComponent> ent, ref LanguageStateChangedEvent args)
    {
        foreach (var key in _viewGenerations.Keys.ToArray())
        {
            if (key.Actor == ent.Owner && TryComp<PaperComponent>(key.Paper, out var paper) &&
                _ui.IsUiOpen(key.Paper, PaperUiKey.Key, ent.Owner))
                SendView((key.Paper, paper), ent.Owner);
        }
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

        if (args.Operations == null || args.Operations.Count > MaxEditOperations)
        {
            SendView(ent, args.Actor, true);
            return;
        }

        var hasLanguage = _languages.TryGetWrittenLanguage(args.Actor, out var language);

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
            var writingLanguage = operation.Language ?? (hasLanguage ? new ProtoId<LanguagePrototype>(language.ID) : Universal);
            if (!string.IsNullOrEmpty(operation.InsertedText) && !_languages.CanWrite(args.Actor, writingLanguage))
            {
                _popup.PopupEntity(Loc.GetString("paper-language-cannot-write"), ent, args.Actor);
                SendView(ent, args.Actor, true);
                return;
            }
            if (operation.InsertedText == null || operation.InsertedText.Length > ent.Comp.ContentSize ||
                operation.Start < 0 || operation.DeleteLength < 0 || operation.Start > currentLength ||
                operation.DeleteLength > currentLength - operation.Start ||
                operation.InsertedText.Length > ent.Comp.ContentSize - insertedBudget ||
                !LanguageDocument.IsWellFormed(operation.InsertedText))
            {
                SendView(ent, args.Actor, true);
                return;
            }
            if (!LanguageDocument.TryEdit(spans, operation, writingLanguage))
            {
                _popup.PopupEntity(Loc.GetString("paper-language-cannot-edit-unknown"), ent, args.Actor);
                SendView(ent, args.Actor);
                return;
            }
            insertedBudget += operation.InsertedText.Length;
            currentLength = currentLength - operation.DeleteLength + operation.InsertedText.Length;
        }

        var content = new StringBuilder();
        var segments = new List<LanguageTextRange>();
        foreach (var span in spans)
        {
            content.Append(span.SourceText);
            LanguageDocument.AppendRange(segments, content.Length - span.SourceText.Length, span.SourceText.Length, span.Language);
        }
        if (content.Length > ent.Comp.ContentSize || !LanguageDocument.IsWellFormed(content.ToString()) ||
            !LanguageDocument.Covers(segments, content.Length))
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
            data.Segments = LanguageDocument.ForText(ent.Comp.Content, Universal);
            data.Revision++;
        }
        else if (preserve == 1)
            _preserveSegments.Remove(ent.Owner);
        else
            _preserveSegments[ent.Owner] = preserve - 1;

        foreach (var actor in _ui.GetActors(ent.Owner, PaperUiKey.Key).ToList())
            SendView(ent, actor);
    }

    public void SetContent(
        Entity<PaperComponent> ent,
        string content,
        IEnumerable<LanguageTextRange> segments)
    {
        var data = EnsureLanguageData(ent);
        if (!LanguageDocument.IsWellFormed(content))
            throw new ArgumentException("Paper content must contain well-formed UTF-16.", nameof(content));
        data.Segments = segments.Take(MaxLanguageSegments)
            .Select(segment => new LanguageTextRange(
                segment.Start,
                segment.Length,
                _prototypes.HasIndex(segment.Language) ? segment.Language : Universal))
            .ToList();
        LanguageDocument.Normalize(data.Segments, content.Length);
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

    public List<LanguageTextRange> CopySegments(EntityUid paper)
    {
        return TryComp<PaperLanguageComponent>(paper, out var data)
            ? LanguageDocument.Clone(data.Segments)
            : new List<LanguageTextRange>();
    }

    private PaperLanguageComponent EnsureLanguageData(Entity<PaperComponent> ent)
    {
        var data = EnsureComp<PaperLanguageComponent>(ent);
        if (!LanguageDocument.Covers(data.Segments, ent.Comp.Content.Length))
        {
            LanguageDocument.Normalize(data.Segments, ent.Comp.Content.Length);
            data.Revision++;
        }
        return data;
    }

    private void SendView(Entity<PaperComponent> ent, EntityUid actor, bool preserveEditor = false)
    {
        _ui.ServerSendUiMessage(ent.Owner, PaperUiKey.Key, BuildViewMessage(ent, actor, preserveEditor), actor);
    }

    public void RefreshViews(Entity<PaperComponent> ent, bool cancelEditors = false)
    {
        if (cancelEditors)
        {
            _writers.RemoveWhere(key => key.Paper == ent.Owner);
            _pendingWriters.RemoveWhere(key => key.Paper == ent.Owner);
        }

        foreach (var actor in _ui.GetActors(ent.Owner, PaperUiKey.Key).ToList())
            SendView(ent, actor);
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
            new List<StampDisplayInfo>(ent.Comp.StampedBy),
            new List<SignatureDisplayInfo>(ent.Comp.SignedBy), preserveEditor);
    }

    private (string Editable, string Rendered) BuildViews(
        string content,
        List<LanguageTextRange> segments,
        EntityUid reader)
    {
        var editable = new StringBuilder(content.Length);
        var rendered = new StringBuilder(content.Length);
        foreach (var span in BuildEditSpans(content, segments, reader))
        {
            editable.Append(span.ViewText);
            AppendRenderedSpan(rendered, span.ViewText, _prototypes.HasIndex(span.Language) ? span.Language.Id : Universal.Id);
        }
        return (editable.ToString(), rendered.ToString());
    }

    private static void AppendRenderedSpan(StringBuilder output, string text, string language)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!PaperLanguageMarkup.TryGetTagLength(text, i, out var tagLength))
                continue;

            AppendLanguageText(output, text.AsSpan(start, i - start), language);
            output.Append(text, i, tagLength);
            i += tagLength - 1;
            start = i + 1;
        }

        AppendLanguageText(output, text.AsSpan(start), language);
    }

    private static void AppendLanguageText(StringBuilder output, ReadOnlySpan<char> text, string language)
    {
        if (text.Length == 0)
            return;

        output.Append($"[paperlang=\"{FormattedMessage.EscapeStringParameter(language)}\"]");
        output.Append(SanitizeLanguageTags(text.ToString()));
        output.Append("[/paperlang]");
    }

    private List<LanguageTextSpan> BuildEditSpans(
        string content,
        List<LanguageTextRange> segments,
        EntityUid reader)
    {
        return LanguageDocument.Project(content, segments,
            language => _languages.CanRead(reader, language),
            (text, id) => _prototypes.TryIndex(id, out var language) ? _languages.Obfuscate(text, language) : "<?>",
            PaperLanguageMarkup.GetProtectedLength);
    }


    private static string SanitizeLanguageTags(string text)
    {
        return text
            .Replace("[paperlang", "［paperlang", StringComparison.OrdinalIgnoreCase)
            .Replace("[/paperlang", "［/paperlang", StringComparison.OrdinalIgnoreCase);
    }

}
