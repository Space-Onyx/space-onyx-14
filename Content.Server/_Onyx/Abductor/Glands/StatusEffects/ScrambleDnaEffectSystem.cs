// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 SX_7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Humanoid.Identity;
using Content.Shared._Onyx.Abductor.Glands.StatusEffects;
using Content.Shared.Humanoid;
using Content.Shared.Popups;

namespace Content.Server._Onyx.Abductor.Glands.StatusEffects;

public sealed partial class ScrambleDnaEffectSystem : EntitySystem
{

    [Dependency] private HumanoidIdentityScrambleSystem _scramble = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ScrambleDnaEffectComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, ScrambleDnaEffectComponent component, ComponentInit args) =>
        Scramble(uid);

    public void Scramble(EntityUid uid)
    {
        if (!_scramble.TryScramble(uid))
            return;

        _popup.PopupEntity(Loc.GetString("scramble-implant-activated-popup"), uid, uid);
    }
}
