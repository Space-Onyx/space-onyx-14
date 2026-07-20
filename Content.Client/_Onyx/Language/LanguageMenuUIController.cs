using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;

namespace Content.Client._Onyx.Language;

[UsedImplicitly]
public sealed class LanguageMenuUIController : UIController, IOnStateChanged<GameplayState>
{
    private LanguageMenuWindow? _window;
    private MenuButton? LanguageButton => UIManager
        .GetActiveUIWidgetOrNull<UserInterface.Systems.MenuBar.Widgets.GameTopMenuBar>()?.LanguageButton;

    public void OnStateEntered(GameplayState state)
    {
        _window = UIManager.CreateWindow<LanguageMenuWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);
        _window.OnOpen += UpdateButton;
        _window.OnClose += UpdateButton;
        CommandBinds.Builder.Bind(ContentKeyFunctions.OpenLanguageMenu,
            InputCmdHandler.FromDelegate(_ => Toggle())).Register<LanguageMenuUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<LanguageMenuUIController>();
        _window?.Dispose();
        _window = null;
    }

    public void LoadButton()
    {
        if (LanguageButton != null)
            LanguageButton.OnPressed += OnPressed;
    }

    public void UnloadButton()
    {
        if (LanguageButton != null)
            LanguageButton.OnPressed -= OnPressed;
    }

    private void OnPressed(BaseButton.ButtonEventArgs args)
    {
        Toggle();
    }

    private void Toggle()
    {
        if (_window?.IsOpen == true)
            _window.Close();
        else
            _window?.Open();
    }

    private void UpdateButton()
    {
        if (LanguageButton != null)
            LanguageButton.Pressed = _window?.IsOpen == true;
    }
}
