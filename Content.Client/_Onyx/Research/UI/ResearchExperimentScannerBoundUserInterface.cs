// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Research.UI;

public sealed class ResearchExperimentScannerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ResearchExperimentScannerMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ResearchExperimentScannerMenu>();
        _menu.ServerPressed += () => SendMessage(new OpenExperimentServerMenuMessage());
        if (State is ResearchExperimentScannerState state)
            _menu.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchExperimentScannerState scannerState)
            _menu?.UpdateState(scannerState);
    }
}
