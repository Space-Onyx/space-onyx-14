using System.Linq;
using Content.Shared._Onyx.Language;
using Content.Shared.Implants.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Language;

public sealed partial class TranslatorSystem : EntitySystem
{
    [Dependency] private LanguageSystem _languages = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private Content.Shared._Onyx.Language.TranslatorSystem _sharedTranslator = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageSpeakerComponent, TranslateLanguageKnowledgeEvent>(OnCollectLanguages);
        SubscribeLocalEvent<HandheldTranslatorComponent, ComponentShutdown>(OnTranslatorShutdown);
        SubscribeLocalEvent<TranslatorImplantComponent, ComponentShutdown>(OnImplantShutdown);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotRemovedFromContainerMessage>(OnTranslatorRemoved);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotInsertedIntoContainerMessage>(OnImplantInserted);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantRemoved);
    }

    private void OnCollectLanguages(Entity<LanguageSpeakerComponent> ent, ref TranslateLanguageKnowledgeEvent args)
    {
        var translators = new List<BaseTranslatorComponent>();

        if (TryComp<ContainerManagerComponent>(ent, out var manager) && manager.Containers != null)
        {
            foreach (var container in manager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (TryComp<HandheldTranslatorComponent>(contained, out var translator) && translator.Enabled &&
                        translator.LifeStage < ComponentLifeStage.Stopping && _powerCell.HasActivatableCharge(contained))
                        translators.Add(translator);
                }
            }
        }

        if (TryComp<ImplantedComponent>(ent, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                if (TryComp<TranslatorImplantComponent>(implant, out var translator) && translator.Enabled &&
                    translator.LifeStage < ComponentLifeStage.Stopping)
                    translators.Add(translator);
            }
        }

        LanguageKnowledge.ResolveTranslators(translators, args.Knowledge);
    }

    private void OnPowerCellSlotEmpty(Entity<HandheldTranslatorComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        SetTranslatorEnabled(ent, false);
    }

    private void OnPowerCellChanged(Entity<HandheldTranslatorComponent> ent, ref PowerCellChangedEvent args)
    {
        SetTranslatorEnabled(ent, ent.Comp.Enabled && _powerCell.HasActivatableCharge(ent.Owner));
    }

    private void OnItemToggled(Entity<HandheldTranslatorComponent> ent, ref ItemToggledEvent args)
    {
        SetTranslatorEnabled(ent, args.Activated && _powerCell.HasActivatableCharge(ent.Owner));
        if (!ent.Comp.Enabled || args.User == null || !ent.Comp.SetLanguageOnInteract ||
            !_containers.TryGetContainingContainer(ent.Owner, out var container))
            return;
        foreach (var language in ent.Comp.Languages.Keys.OrderBy(id => id.Id, StringComparer.Ordinal))
        {
            if (!_languages.CanSpeak(container.Owner, language) ||
                TryComp<LanguageKnowledgeComponent>(container.Owner, out var intrinsic) &&
                LanguageKnowledge.Has(intrinsic.Languages, language, LanguageAbility.Speak))
                continue;
            _languages.SetLanguage(container.Owner, language);
            break;
        }
    }

    private void SetTranslatorEnabled(Entity<HandheldTranslatorComponent> ent, bool enabled)
    {
        ent.Comp.Enabled = enabled;
        if (!enabled && TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated)
            _toggle.TryDeactivate((ent.Owner, toggle), predicted: false);
        _powerCell.SetDrawEnabled(ent.Owner, enabled);
        _sharedTranslator.UpdateAppearance(ent);

        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            _languages.UpdateLanguages(container.Owner);
    }

    private void OnTranslatorInserted(Entity<HandheldTranslatorComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        _languages.UpdateLanguages(args.Container.Owner);
    }

    private void OnTranslatorShutdown(Entity<HandheldTranslatorComponent> ent, ref ComponentShutdown args)
    {
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            _languages.QueueUpdate(container.Owner);
    }

    private void OnImplantShutdown(Entity<TranslatorImplantComponent> ent, ref ComponentShutdown args)
    {
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            _languages.QueueUpdate(container.Owner);
    }

    private void OnTranslatorRemoved(Entity<HandheldTranslatorComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        UpdateLanguagesDeferred(args.Container.Owner);
    }

    private void OnImplantInserted(Entity<TranslatorImplantComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        _languages.UpdateLanguages(args.Container.Owner);
    }

    private void OnImplantRemoved(Entity<TranslatorImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        UpdateLanguagesDeferred(args.Container.Owner);
    }

    private void UpdateLanguagesDeferred(EntityUid speaker)
    {
        _languages.QueueUpdate(speaker);
    }
}
