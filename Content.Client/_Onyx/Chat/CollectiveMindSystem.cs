// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.CollectiveMind;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client._Onyx.Chat;

public sealed partial class CollectiveMindSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    public event Action? AccessChanged;

    public bool CanHear => _player.LocalEntity is { } uid &&
        TryComp<CollectiveMindComponent>(uid, out var component) &&
        (component.Channels.Count > 0 || component.HearAll);

    public bool CanSend => _player.LocalEntity is { } uid &&
        TryComp<CollectiveMindComponent>(uid, out var component) &&
        component.Channels.Count > 0;

    public override void Initialize()
    {
        SubscribeLocalEvent<CollectiveMindComponent, ComponentInit>(OnChanged);
        SubscribeLocalEvent<CollectiveMindComponent, ComponentRemove>(OnChanged);
        SubscribeLocalEvent<CollectiveMindComponent, AfterAutoHandleStateEvent>(OnChanged);
    }

    private void OnChanged(Entity<CollectiveMindComponent> ent, ref ComponentInit args)
    {
        if (ent.Owner == _player.LocalEntity)
            AccessChanged?.Invoke();
    }

    private void OnChanged(Entity<CollectiveMindComponent> ent, ref ComponentRemove args)
    {
        if (ent.Owner == _player.LocalEntity)
            AccessChanged?.Invoke();
    }

    private void OnChanged(Entity<CollectiveMindComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
            AccessChanged?.Invoke();
    }
}
