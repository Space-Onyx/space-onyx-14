// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Religion;
using Content.Shared.Bible.Components;
using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Timing;

namespace Content.Shared.Bible;

public sealed partial class BibleSystem
{
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly DamageSpecifier CosmicCultSmiteDamage = new()
    {
        DamageDict = new() { ["Holy"] = 25 },
    };

    private static readonly TimeSpan CosmicCultSmiteStun = TimeSpan.FromSeconds(8);

    private bool TryDoCosmicCultSmite(
        Entity<BibleComponent> bible,
        EntityUid performer,
        EntityUid target,
        UseDelayComponent useDelay)
    {
        if (!TryComp<WeakToHolyComponent>(target, out var weakness) ||
            !weakness.AlwaysTakeHoly ||
            !HasComp<BibleUserComponent>(performer))
            return false;

        _popup.PopupEntity(
            Loc.GetString("weaktoholy-component-bible-sizzle",
                ("target", Identity.Entity(target, EntityManager)),
                ("item", Identity.Entity(bible, EntityManager))),
            target,
            PopupType.LargeCaution);
        _audio.PlayPredicted(bible.Comp.SizzleSound, target, performer);
        _damageable.TryChangeDamage(target, CosmicCultSmiteDamage, origin: bible);
        _stun.TryUpdateParalyzeDuration(target, CosmicCultSmiteStun);
        _delay.TryResetDelay((bible, useDelay));
        return true;
    }
}
