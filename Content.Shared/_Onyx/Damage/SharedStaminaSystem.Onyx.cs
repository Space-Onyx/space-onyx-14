// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Onyx.Stunnable;

namespace Content.Shared.Damage.Systems;

public abstract partial class SharedStaminaSystem
{
    private void ApplyStaminaOvertime(EntityUid uid, float value)
    {
        if (value == 0f)
            return;

        var hasComp = TryComp<OvertimeStaminaDamageComponent>(uid, out var overtime);
        if (!hasComp)
            overtime = EnsureComp<OvertimeStaminaDamageComponent>(uid);

        overtime!.Amount = hasComp ? overtime.Amount + value : value;
        overtime!.Damage = hasComp ? overtime.Damage + value : value;
    }
}
