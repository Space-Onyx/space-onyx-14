// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.UserInterface.Fragments;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.CartridgeLoader.Cartridges;

public sealed partial class OnyxNanoChatUi : UIFragment
{
    private OnyxNanoChatUiFragment? _fragment;
    private BoundUserInterface? _userInterface;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (_fragment is { Disposed: false } && ReferenceEquals(_userInterface, userInterface))
            return;

        _fragment?.Dispose();
        _userInterface = userInterface;
        _fragment = new OnyxNanoChatUiFragment();

        _fragment.OnMessageSent += (type, number, content, job) =>
        {
            SendNanoChatUiMessage(type, number, content, job, userInterface);
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is NanoChatUiState cast)
            _fragment?.UpdateState(cast);
    }

    private static void SendNanoChatUiMessage(NanoChatUiMessageType type,
        uint? number,
        string? content,
        string? job,
        BoundUserInterface userInterface)
    {
        var nanoChatMessage = new NanoChatUiMessageEvent(type, number, content, job);
        var message = new CartridgeUiMessage(nanoChatMessage);
        userInterface.SendMessage(message);
    }
}
