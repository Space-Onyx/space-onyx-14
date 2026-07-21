// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.SetSelector;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.SetSelector;

public sealed class SetSelectorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SetSelectorMenu? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SetSelectorMenu>();
        _window.OnApprove += () => SendMessage(new SetSelectorApproveMessage());
        _window.OnSetChange += set => SendMessage(new SetSelectorChangeSetMessage(set));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is SetSelectorBoundUserInterfaceState current)
            _window?.UpdateState(current);
    }
}
