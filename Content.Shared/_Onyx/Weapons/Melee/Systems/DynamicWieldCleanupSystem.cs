// SPDX-FileCopyrightText: 2026 Onyx
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Weapons.Melee.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Item;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Onyx.Weapons.Melee.Systems;

public sealed partial class DynamicWieldCleanupSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WieldableComponent, ComponentShutdown>(OnWieldableShutdown);
    }

    private void OnWieldableShutdown(Entity<WieldableComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent, out DynamicWieldCleanupComponent? cleanup) || TerminatingOrDeleted(ent))
            return;

        var holder = Transform(ent).ParentUid;
        if (ent.Comp.Wielded && HasComp<HandsComponent>(holder))
            _wieldable.TryUnwield(ent.AsNullable(), holder, force: true);

        _item.SetHeldPrefix(ent, cleanup.FoldedInhandPrefix);
    }
}
