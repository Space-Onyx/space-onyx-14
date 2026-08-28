using System.Linq;
using Content.Server.Popups;
using Content.Shared._Onyx.Language;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Language;

public sealed partial class TranslatorSystem : EntitySystem
{
    [Dependency] private LanguageSystem _languages = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private Content.Shared._Onyx.Language.TranslatorSystem _sharedTranslator = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageSpeakerComponent, CollectLanguageKnowledgeEvent>(OnCollectLanguages);
        SubscribeLocalEvent<HandheldTranslatorComponent, ActivateInWorldEvent>(OnTranslatorToggle);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotRemovedFromContainerMessage>(OnTranslatorRemoved);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotInsertedIntoContainerMessage>(OnImplantInserted);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantRemoved);
    }

    private void OnCollectLanguages(Entity<LanguageSpeakerComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        var translators = new List<BaseTranslatorComponent>();

        if (TryComp<ContainerManagerComponent>(ent, out var manager) && manager.Containers != null)
        {
            foreach (var container in manager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (TryComp<HandheldTranslatorComponent>(contained, out var translator))
                        translators.Add(translator);
                }
            }
        }

        if (TryComp<ImplantedComponent>(ent, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                if (TryComp<TranslatorImplantComponent>(implant, out var translator))
                    translators.Add(translator);
            }
        }

        var spoken = TryComp<LanguageKnowledgeComponent>(ent, out var intrinsic)
            ? intrinsic.SpokenLanguages
            : [];
        var understood = intrinsic?.UnderstoodLanguages ?? [];
        AddTranslatorLanguages(translators, spoken, understood, args);
    }

    private static void AddTranslatorLanguages(
        List<BaseTranslatorComponent> translators,
        ICollection<ProtoId<LanguagePrototype>> intrinsicSpoken,
        ICollection<ProtoId<LanguagePrototype>> intrinsicUnderstood,
        CollectLanguageKnowledgeEvent knowledge)
    {
        var spoken = new HashSet<ProtoId<LanguagePrototype>>(intrinsicSpoken);
        var understood = new HashSet<ProtoId<LanguagePrototype>>(intrinsicUnderstood);
        var remaining = translators.Where(translator => translator.Enabled).ToList();

        while (remaining.Count > 0)
        {
            var changed = false;
            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var translator = remaining[i];
                var canSpeak = RequirementsMet(translator.RequiredLanguages, spoken, translator.RequiresAllLanguages);
                var canUnderstand = RequirementsMet(translator.RequiredLanguages, understood, translator.RequiresAllLanguages);
                if (!canSpeak && !canUnderstand)
                    continue;

                if (canSpeak)
                    spoken.UnionWith(translator.SpokenLanguages);
                if (canUnderstand)
                    understood.UnionWith(translator.UnderstoodLanguages);

                remaining.RemoveAt(i);
                changed = true;
            }

            if (!changed)
                break;
        }

        knowledge.SpokenLanguages.UnionWith(spoken);
        knowledge.UnderstoodLanguages.UnionWith(understood);
    }

    public static bool RequirementsMet<T>(ICollection<T> required, ICollection<T> provided, bool requireAll)
    {
        return required.Count == 0 ||
               (requireAll ? required.All(provided.Contains) : required.Any(provided.Contains));
    }

    private void OnTranslatorToggle(Entity<HandheldTranslatorComponent> ent, ref ActivateInWorldEvent args)
    {
        var hasPower = _powerCell.HasDrawCharge(ent.Owner, user: args.User);
        var enabled = !ent.Comp.Enabled && hasPower;
        ent.Comp.Enabled = enabled;
        _powerCell.SetDrawEnabled(ent.Owner, enabled);
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
        {
            var holder = container.Owner;
            _languages.UpdateLanguages(holder);
            if (enabled && ent.Comp.SetLanguageOnInteract)
            {
                var newLanguage = ent.Comp.SpokenLanguages.FirstOrDefault(language =>
                    !TryComp<LanguageKnowledgeComponent>(holder, out var knowledge) ||
                    !knowledge.SpokenLanguages.Contains(language));
                if (newLanguage != default)
                    _languages.SetLanguage(holder, newLanguage);
            }
        }

        _sharedTranslator.UpdateAppearance(ent);
        if (hasPower)
        {
            var message = Loc.GetString(enabled ? "translator-component-turnon" : "translator-component-shutoff",
                ("translator", ent.Owner));
            _popup.PopupEntity(message, ent.Owner, args.User);
        }

        args.Handled = true;
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
    }

    private void SetTranslatorEnabled(Entity<HandheldTranslatorComponent> ent, bool enabled)
    {
        ent.Comp.Enabled = enabled;
        _powerCell.SetDrawEnabled(ent.Owner, enabled);
        _sharedTranslator.UpdateAppearance(ent);

        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            _languages.UpdateLanguages(container.Owner);
    }

    private void OnTranslatorInserted(Entity<HandheldTranslatorComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        _languages.UpdateLanguages(args.Container.Owner);
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
        Timer.Spawn(0, () =>
        {
            if (Exists(speaker))
                _languages.UpdateLanguages(speaker);
        });
    }
}
