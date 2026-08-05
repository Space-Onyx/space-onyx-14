using Content.Server._Onyx.Language;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Traits;

namespace Content.Server._Onyx.Traits;

public sealed partial class LanguageTraitSystem : EntitySystem
{
    [Dependency] private LanguageSystem _languages = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageTraitComponent, CollectLanguageKnowledgeEvent>(OnCollectKnowledge);
        SubscribeLocalEvent<LanguageTraitComponent, ComponentStartup>(OnStartup);
    }

    private void OnCollectKnowledge(Entity<LanguageTraitComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        args.SpokenLanguages.Add(ent.Comp.Language);
        args.UnderstoodLanguages.Add(ent.Comp.Language);
    }

    private void OnStartup(Entity<LanguageTraitComponent> ent, ref ComponentStartup args)
    {
        _languages.UpdateLanguages(ent.Owner);
    }
}
