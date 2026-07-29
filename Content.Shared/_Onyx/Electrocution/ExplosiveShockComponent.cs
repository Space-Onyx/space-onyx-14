// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Shared._Onyx.Electrocution;

[RegisterComponent]
public sealed partial class ExplosiveShockComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier HandsDamage = default!;

    [DataField(required: true)]
    public DamageSpecifier ArmsDamage = default!;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ExplosionDelay = TimeSpan.FromSeconds(1);
}

[RegisterComponent]
public sealed partial class ExplosiveShockIgnitedComponent : Component
{
    public TimeSpan ExplodeAt;
}
