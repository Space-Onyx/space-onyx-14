// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.Abductor;

[RegisterComponent]
public sealed partial class InjectOnHitComponent : Component
{
    [DataField(required: true)]
    public List<ReagentQuantity> Reagents = [];

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float? Limit;

    [DataField]
    public bool NeedsRestrain;

    [DataField]
    public int InjectionDelay = 10000;
}
