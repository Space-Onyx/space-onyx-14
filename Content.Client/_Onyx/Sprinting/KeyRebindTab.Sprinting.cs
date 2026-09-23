// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.Options.UI.Tabs;

public sealed partial class KeyRebindTab
{
    private void InitToggleSprint()
    {
        if (_cfg.GetCVar(CCVars.ToggleSprint) && !_cfg.GetCVar(CCVars.SprintUntilStop))
            ToggleFunctions.Add(ContentKeyFunctions.Sprint);
        else
            ToggleFunctions.Remove(ContentKeyFunctions.Sprint);
    }

    private void HandleToggleSprint(BaseButton.ButtonToggledEventArgs args)
    {
        _cfg.SetCVar(CCVars.ToggleSprint, args.Pressed);
        _cfg.SaveToFile();
        InitToggleSprint();
        UpdateSprintBindings();
    }

    private void HandleSprintUntilStop(BaseButton.ButtonToggledEventArgs args)
    {
        _cfg.SetCVar(CCVars.SprintUntilStop, args.Pressed);
        _cfg.SaveToFile();
        InitToggleSprint();
        UpdateSprintBindings();
    }

    private void UpdateSprintBindings()
    {
        if (!_keyControls.TryGetValue(ContentKeyFunctions.Sprint, out var keyControl))
            return;

        var bindingType = ToggleFunctions.Contains(ContentKeyFunctions.Sprint)
            ? KeyBindingType.Toggle
            : KeyBindingType.State;
        for (var i = 0; i <= 1; i++)
        {
            var binding = (i == 0 ? keyControl.BindButton1 : keyControl.BindButton2).Binding;
            if (binding == null)
                continue;

            var registration = new KeyBindingRegistration
            {
                Function = ContentKeyFunctions.Sprint,
                BaseKey = binding.BaseKey,
                Mod1 = binding.Mod1,
                Mod2 = binding.Mod2,
                Mod3 = binding.Mod3,
                Priority = binding.Priority,
                Type = bindingType,
                CanFocus = binding.CanFocus,
                CanRepeat = binding.CanRepeat,
            };

            _deferCommands.Add(() =>
            {
                _inputManager.RemoveBinding(binding);
                _inputManager.RegisterBinding(registration);
            });
        }

        _deferCommands.Add(_inputManager.SaveToUserData);
    }
}
