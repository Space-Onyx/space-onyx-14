// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Blob;
using Content.Shared._Onyx.Blob.Components;
using Content.Shared.GameTicking;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Graphics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Blob;

public sealed partial class BlobObserverSystem : SharedBlobObserverSystem
{
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobObserverComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BlobObserverComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<BlobObserverComponent, GetStatusIconsEvent>(OnShowBlobIcon);
        SubscribeLocalEvent<BlobCarrierComponent, GetStatusIconsEvent>(OnShowBlobIcon);
        SubscribeLocalEvent<BlobMobComponent, GetStatusIconsEvent>(OnShowBlobIcon);

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
    }

    private static readonly ProtoId<FactionIconPrototype> BlobFaction = "BlobFaction";

    private void OnShowBlobIcon<T>(Entity<T> ent, ref GetStatusIconsEvent args) where T : Component
    {
        args.StatusIcons.Add(_prototype.Index<FactionIconPrototype>(BlobFaction));
    }

    private void OnPlayerAttached(EntityUid uid, BlobObserverComponent component, LocalPlayerAttachedEvent args)
    {
        _lightManager.Enabled = false;
    }

    private void OnPlayerDetached(EntityUid uid, BlobObserverComponent component, LocalPlayerDetachedEvent args)
    {
        _lightManager.Enabled = true;
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lightManager.Enabled = true;
    }
}
