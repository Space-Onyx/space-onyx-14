// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Ghost;

public sealed partial class GhostReturnToLobbySlotSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IChatManager _chat = default!;

    private readonly Dictionary<NetUserId, HashSet<int>> _usedSlots = new();
    private readonly Dictionary<NetUserId, int> _spawnedSlots = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public bool CanUseCharacter(NetUserId userId, int characterSlot)
    {
        return !_usedSlots.TryGetValue(userId, out var slots) || !slots.Contains(characterSlot);
    }

    public bool TryRecordSpawn(ICommonSession player)
    {
        try
        {
            var slot = _prefs.GetPreferences(player.UserId).SelectedCharacterIndex;
            if (!CanUseCharacter(player.UserId, slot))
            {
                _chat.DispatchServerMessage(player, Loc.GetString("ghost-return-to-lobby-used"));
                return false;
            }

            _spawnedSlots[player.UserId] = slot;
            return true;
        }
        catch
        {
            return true;
        }
    }

    public void MarkReturnToLobby(ICommonSession player)
    {
        if (!EntityManager.System<GameTicker>().LobbyEnabled)
            return;

        try
        {
            if (!_spawnedSlots.TryGetValue(player.UserId, out var slot))
                return;

            if (!_usedSlots.TryGetValue(player.UserId, out var slots))
                _usedSlots[player.UserId] = slots = new HashSet<int>();

            slots.Add(slot);
        }
        catch
        {
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PreRoundLobby)
        {
            _usedSlots.Clear();
            _spawnedSlots.Clear();
        }
    }
}
