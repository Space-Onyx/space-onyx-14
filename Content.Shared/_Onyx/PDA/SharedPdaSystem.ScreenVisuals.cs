// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.PDA;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Utility;

namespace Content.Shared.PDA;

public abstract partial class SharedPdaSystem
{
    private void OnCartridgeActivated(Entity<CartridgeComponent> cartridge, ref CartridgeActivatedEvent args)
    {
        if (!TryComp<PdaScreenVisualsComponent>(args.Loader.Owner, out var visuals))
            return;

        Appearance.SetData(args.Loader.Owner, PdaVisuals.ScreenState, cartridge.Comp.ScreenState ?? visuals.IdleScreen);
    }

    private void OnCartridgeDeactivated(Entity<CartridgeComponent> cartridge, ref CartridgeDeactivatedEvent args)
    {
        if (TryComp<PdaScreenVisualsComponent>(args.Loader.Owner, out var visuals))
            Appearance.SetData(args.Loader.Owner, PdaVisuals.ScreenState, visuals.MenuScreen);
    }

    protected bool TryGetPdaScreen(EntityUid uid, bool showMenu, out SpriteSpecifier screen)
    {
        screen = default!;
        if (!TryComp<PdaScreenVisualsComponent>(uid, out var visuals))
            return false;

        if (TryComp<CartridgeLoaderComponent>(uid, out var loader) &&
            loader.ActiveProgram is { } active &&
            TryComp<CartridgeComponent>(active, out var cartridge) &&
            cartridge.ScreenState is { } programScreen)
        {
            screen = programScreen;
            return true;
        }

        screen = showMenu ? visuals.MenuScreen : visuals.IdleScreen;
        return true;
    }
}
