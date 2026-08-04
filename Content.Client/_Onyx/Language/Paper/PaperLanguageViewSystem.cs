using Content.Client.Paper.UI;
using Content.Shared._Onyx.Language.Paper;
using Content.Shared.Paper;

namespace Content.Client._Onyx.Language.Paper;

public sealed partial class PaperLanguageViewSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, PaperLanguageViewMessage> _prefetched = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<PaperLanguageViewPrefetchEvent>(OnPrefetch);
        SubscribeLocalEvent<PaperComponent, ComponentShutdown>(OnPaperShutdown);
    }

    private void OnPrefetch(PaperLanguageViewPrefetchEvent args)
    {
        var paper = GetEntity(args.Paper);
        if (_prefetched.TryGetValue(paper, out var current) && current.ViewGeneration > args.View.ViewGeneration)
            return;
        _prefetched[paper] = args.View;
    }

    private void OnPaperShutdown(Entity<PaperComponent> ent, ref ComponentShutdown args)
    {
        _prefetched.Remove(ent.Owner);
    }

    public void PopulatePrefetched(EntityUid paper, PaperWindow window)
    {
        if (!_prefetched.Remove(paper, out var view))
            return;

        window.PopulateLanguage(WithPredictedMode(paper, view));
    }

    public void Store(EntityUid paper, PaperLanguageViewMessage view)
    {
        if (_prefetched.TryGetValue(paper, out var current) && current.ViewGeneration > view.ViewGeneration)
            return;
        _prefetched[paper] = view;
    }

    private PaperLanguageViewMessage WithPredictedMode(EntityUid paper, PaperLanguageViewMessage view)
    {
        if (!TryComp<PaperComponent>(paper, out var component) || component.Mode == view.Mode)
            return view;

        return new PaperLanguageViewMessage(
            view.Text,
            view.EditableText,
            view.Revision,
            view.ViewGeneration,
            component.Mode,
            view.StampedBy,
            view.PreserveEditor);
    }

}
