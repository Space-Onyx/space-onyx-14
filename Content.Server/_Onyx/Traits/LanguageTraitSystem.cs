using Content.Server._Onyx.Language;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Traits;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Traits;

public sealed partial class LanguageTraitSystem : EntitySystem
{
    [Dependency] private LanguageSystem _languages = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageTraitComponent, CollectLanguageKnowledgeEvent>(OnCollectKnowledge);
        SubscribeLocalEvent<LanguageTraitComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LanguageTraitComponent, ComponentRemove>(OnRemoved);
    }

    private void OnCollectKnowledge(Entity<LanguageTraitComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        args.SpokenLanguages.UnionWith(ent.Comp.Languages);
        args.UnderstoodLanguages.UnionWith(ent.Comp.Languages);
    }

    private void OnStartup(Entity<LanguageTraitComponent> ent, ref ComponentStartup args)
    {
        _languages.UpdateLanguages(ent.Owner);
    }

    private void OnRemoved(Entity<LanguageTraitComponent> ent, ref ComponentRemove args)
    {
        Timer.Spawn(0, () =>
        {
            if (Exists(ent.Owner))
                _languages.UpdateLanguages(ent.Owner);
        });
    }
}
