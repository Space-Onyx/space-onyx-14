// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Server.Chat.Systems;
using Content.Shared._Onyx.Hailer;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Hailer;

public sealed partial class HailerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly string[] Sounds =
    [
        "/Audio/_Onyx/Hailer/asshole.ogg",
        "/Audio/_Onyx/Hailer/bash.ogg",
        "/Audio/_Onyx/Hailer/bobby.ogg",
        "/Audio/_Onyx/Hailer/compliance.ogg",
        "/Audio/_Onyx/Hailer/dontmove.ogg",
        "/Audio/_Onyx/Hailer/dredd.ogg",
        "/Audio/_Onyx/Hailer/floor.ogg",
        "/Audio/_Onyx/Hailer/freeze.ogg",
        "/Audio/_Onyx/Hailer/halt.ogg",
    ];

    private static readonly TimeSpan FixedDelay = TimeSpan.FromSeconds(2);

    private readonly Dictionary<EntityUid, TimeSpan> _delays = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, HailerActionEvent>(OnHail);
        SubscribeLocalEvent<HailerComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HailerComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid uid, HailerComponent component, GotEquippedEvent args)
    {
        if (args.SlotFlags == SlotFlags.MASK)
        {
            _actions.AddAction(args.EquipTarget, ref component.HailActionEntity, component.HailerAction, args.EquipTarget);
        }
    }

    private void OnGotUnequipped(EntityUid uid, HailerComponent component, GotUnequippedEvent args)
    {
        if (args.SlotFlags == SlotFlags.MASK)
        {
            _actions.RemoveAction(args.EquipTarget, component.HailActionEntity);
        }
    }

    private void OnHail(EntityUid uid, ActionsComponent component, ref HailerActionEvent args)
    {
        if (args.Handled)
            return;
        // No hail spam check.
        if (_delays.TryGetValue(uid, out var delay))
        {
            if (_timing.CurTime < delay)
            {
                return;
            }
        }
        var rInt = (int) _random.NextDouble(0, Sounds.Length);
        _audio.PlayPvs(Sounds[rInt], uid);
        _delays[uid] = _timing.CurTime.Add(FixedDelay);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("hail-" + rInt), InGameICChatType.Speak, ChatTransmitRange.GhostRangeLimit, nameOverride: Name(uid) + "(SecMask)", checkRadioPrefix: false);
    }
}
