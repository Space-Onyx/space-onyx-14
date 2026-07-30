// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Bible.Components;
using Content.Shared._Onyx.Religion;
using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Timing;

namespace Content.Server.Bible;

public sealed partial class BibleSystem
{
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly DamageSpecifier CosmicCultSmiteDamage = new()
    {
        DamageDict = new() { ["Holy"] = 25 },
    };

    private static readonly TimeSpan CosmicCultSmiteStun = TimeSpan.FromSeconds(8);

    private bool TryDoCosmicCultSmite(
        EntityUid bible,
        EntityUid performer,
        EntityUid target,
        UseDelayComponent useDelay,
        BibleComponent component)
    {
        if (!TryComp<WeakToHolyComponent>(target, out var weakness) ||
            !weakness.AlwaysTakeHoly ||
            !HasComp<BibleUserComponent>(performer))
            return false;

        _popupSystem.PopupEntity(
            Loc.GetString("weaktoholy-component-bible-sizzle",
                ("target", Identity.Entity(target, EntityManager)),
                ("item", Identity.Entity(bible, EntityManager))),
            target,
            PopupType.LargeCaution);
        _audio.PlayPvs(component.SizzleSoundPath, target);
        _damageableSystem.TryChangeDamage(target, CosmicCultSmiteDamage, origin: bible);
        _stun.TryUpdateParalyzeDuration(target, CosmicCultSmiteStun);
        _delay.TryResetDelay((bible, useDelay));
        return true;
    }
}
