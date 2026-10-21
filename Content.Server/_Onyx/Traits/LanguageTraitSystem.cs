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
        SubscribeLocalEvent<LanguageTraitComponent, ComponentRemove>(OnRemoved);
    }

    private void OnCollectKnowledge(Entity<LanguageTraitComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;
        foreach (var language in ent.Comp.Languages)
            args.Grant(language, LanguageAbility.All);
    }

    private void OnStartup(Entity<LanguageTraitComponent> ent, ref ComponentStartup args)
    {
        _languages.UpdateLanguages(ent.Owner);
    }

    private void OnRemoved(Entity<LanguageTraitComponent> ent, ref ComponentRemove args)
    {
        _languages.QueueUpdate(ent);
    }
}
