// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    public float GetRange(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        var ev = new GetMeleeRangeEvent(uid, component.Range, 1, user);
        RaiseLocalEvent(uid, ref ev);
        return ev.Range * ev.Multipliers;
    }
}
