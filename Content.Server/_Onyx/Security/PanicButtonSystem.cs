// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Security;

public sealed partial class PanicButtonSystem : EntitySystem
{
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PanicButtonComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<PanicButtonComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_useDelay.TryResetDelay(ent.Owner, checkDelayed: true))
            return;

        var position = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent.Owner));
        var message = Loc.GetString(ent.Comp.DistressMessage, ("position", position));
        _radio.SendRadioMessage(ent.Owner, message, ent.Comp.RadioChannel, ent.Owner);
        args.Handled = true;
    }
}
