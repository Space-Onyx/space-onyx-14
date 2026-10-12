// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Gravity;
using Content.Shared.Standing;

namespace Content.Shared._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleGravitySystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleAntiGravityEffectComponent, IsWeightlessEvent>(OnAntiGravity);
        SubscribeLocalEvent<ModModuleAntiGravityEffectComponent, ComponentStartup>(OnAntiGravityStarted);
        SubscribeLocalEvent<ModModuleAntiGravityEffectComponent, ComponentRemove>(OnAntiGravityRemoved);
        SubscribeLocalEvent<ModModuleAntiGravityEffectComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<ModModuleAntiGravityEffectComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<ModModuleMagneticEffectComponent, IsWeightlessEvent>(OnMagnetic);
        SubscribeLocalEvent<ModModuleMagneticEffectComponent, ComponentStartup>(OnMagneticStarted);
        SubscribeLocalEvent<ModModuleMagneticEffectComponent, ComponentRemove>(OnMagneticRemoved);
    }

    private void OnAntiGravity(Entity<ModModuleAntiGravityEffectComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled || _standing.IsDown(ent.Owner) ||
            HasComp<ModModuleMagneticEffectComponent>(ent) && _gravity.EntityOnGravitySupportingGridOrMap(ent.Owner))
            return;
        args.IsWeightless = true;
        args.Handled = true;
    }

    private void OnMagnetic(Entity<ModModuleMagneticEffectComponent> ent, ref IsWeightlessEvent args)
    {
        if (!_gravity.EntityOnGravitySupportingGridOrMap(ent.Owner))
            return;
        args.IsWeightless = false;
        args.Handled = true;
    }

    private void OnAntiGravityStarted(Entity<ModModuleAntiGravityEffectComponent> ent, ref ComponentStartup args) => _gravity.RefreshWeightless(ent.Owner);
    private void OnAntiGravityRemoved(Entity<ModModuleAntiGravityEffectComponent> ent, ref ComponentRemove args) => _gravity.RefreshWeightless(ent.Owner);
    private void OnDowned(Entity<ModModuleAntiGravityEffectComponent> ent, ref DownedEvent args) => _gravity.RefreshWeightless(ent.Owner);
    private void OnStood(Entity<ModModuleAntiGravityEffectComponent> ent, ref StoodEvent args) => _gravity.RefreshWeightless(ent.Owner);
    private void OnMagneticStarted(Entity<ModModuleMagneticEffectComponent> ent, ref ComponentStartup args) => _gravity.RefreshWeightless(ent.Owner);
    private void OnMagneticRemoved(Entity<ModModuleMagneticEffectComponent> ent, ref ComponentRemove args) => _gravity.RefreshWeightless(ent.Owner);
}
