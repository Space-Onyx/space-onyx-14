using Content.Shared._Onyx.Language;
using Robust.Client.Player;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Language;

public sealed partial class LanguageSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    public event Action? LanguagesChanged;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentHandleState>(OnHandleState);
        _player.LocalPlayerAttached += _ => LanguagesChanged?.Invoke();
    }

    private void OnHandleState(Entity<LanguageSpeakerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not LanguageSpeakerComponentState state)
            return;

        ent.Comp.CurrentLanguage = state.CurrentLanguage;
        ent.Comp.SpokenLanguages = state.SpokenLanguages;
        ent.Comp.UnderstoodLanguages = state.UnderstoodLanguages;
        ent.Comp.UnderstandsAllLanguages = state.UnderstandsAllLanguages;

        if (ent.Owner == _player.LocalEntity)
            LanguagesChanged?.Invoke();
    }

    public LanguageSpeakerComponent? GetLocalSpeaker()
    {
        return CompOrNull<LanguageSpeakerComponent>(_player.LocalEntity);
    }

    public void SelectLanguage(ProtoId<LanguagePrototype> language)
    {
        if (GetLocalSpeaker()?.CurrentLanguage != language)
            RaiseNetworkEvent(new SetLanguageMessage(language));
    }
}
