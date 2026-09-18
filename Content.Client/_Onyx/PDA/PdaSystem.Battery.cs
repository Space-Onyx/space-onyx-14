// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.PDA;
using Content.Shared.Light;
using Content.Shared.Light.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.UserInterface;

namespace Content.Client.PDA;

public sealed partial class PdaSystem
{
    [Dependency] private PowerCellSystem _pdaCell = default!;
    [Dependency] private UnpoweredFlashlightSystem _pdaFlashlight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PdaBatteryComponent, ActivatableUIOpenAttemptEvent>(OnPdaBatteryOpenAttempt);
        SubscribeLocalEvent<PdaBatteryComponent, LightToggleEvent>(OnPdaBatteryLight);
    }

    private void OnPdaBatteryOpenAttempt(Entity<PdaBatteryComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_pdaCell.HasCharge(ent.Owner, 1f, args.Silent ? null : args.User, true))
            args.Cancel();
    }

    private void OnPdaBatteryLight(Entity<PdaBatteryComponent> ent, ref LightToggleEvent args)
    {
        if (args.IsOn && !_pdaCell.HasCharge(ent.Owner, 1f))
            _pdaFlashlight.SetLight(ent.Owner, false);
    }
}
