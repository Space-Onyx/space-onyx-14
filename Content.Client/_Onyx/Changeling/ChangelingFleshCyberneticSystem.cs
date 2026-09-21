// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Changeling;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Changeling;

public sealed partial class ChangelingFleshCyberneticSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingFleshCyberneticComponent, AfterAutoHandleStateEvent>(OnChanged);
        SubscribeLocalEvent<ChangelingFleshCyberneticComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnChanged(Entity<ChangelingFleshCyberneticComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    private void OnShutdown(Entity<ChangelingFleshCyberneticComponent> ent, ref ComponentShutdown args)
    {
        if (Prototype(ent) is { } prototype)
            CopySprite(prototype, ent);
    }

    private void UpdateSprite(Entity<ChangelingFleshCyberneticComponent> ent)
    {
        if (_prototypes.TryIndex(ent.Comp.Prototype, out EntityPrototype? prototype))
            CopySprite(prototype, ent);
    }

    private void CopySprite(EntityPrototype prototype, EntityUid target)
    {
        if (!prototype.TryGetComponent(out SpriteComponent? source, Factory) ||
            !TryComp(target, out SpriteComponent? sprite))
            return;

        sprite.CopyFrom(source);
    }
}
