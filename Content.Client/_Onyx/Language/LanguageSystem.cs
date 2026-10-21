using Content.Shared._Onyx.Language;
using Robust.Client.Player;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private IPlayerManager _player = default!;

    public event Action? LanguagesChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnHandleState);
        _player.LocalPlayerAttached += OnPlayerChanged;
        _player.LocalPlayerDetached += OnPlayerChanged;
    }

    public override void Shutdown()
    {
        _player.LocalPlayerAttached -= OnPlayerChanged;
        _player.LocalPlayerDetached -= OnPlayerChanged;
        base.Shutdown();
    }

    private void OnPlayerChanged(EntityUid entity) => LanguagesChanged?.Invoke();

    private void OnHandleState(Entity<LanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var changed = new LanguageStateChangedEvent();
        RaiseLocalEvent(ent.Owner, ref changed);
        if (ent.Owner == _player.LocalEntity)
            LanguagesChanged?.Invoke();
    }

    public LanguageSpeakerComponent? GetLocalSpeaker()
    {
        return CompOrNull<LanguageSpeakerComponent>(_player.LocalEntity);
    }

    public void SelectLanguage(ProtoId<LanguagePrototype> language, bool writing = false)
    {
        if (_player.LocalEntity is not { } speaker ||
            !(writing ? CanWrite(speaker, language) : CanSpeak(speaker, language)))
            return;
        RaiseNetworkEvent(new SetLanguageMessage(language, writing));
    }
}
