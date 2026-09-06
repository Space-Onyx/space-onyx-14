// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Research.UI;

public sealed class ResearchExperimentMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ResearchExperimentMachineMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ResearchExperimentMachineMenu>();
        _menu.ServerPressed += () => SendMessage(new OpenExperimentServerMenuMessage());
        _menu.RunPressed += () => SendMessage(new RunResearchExperimentMessage());
        if (State is ResearchExperimentMachineBuiState state)
            _menu.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchExperimentMachineBuiState machineState)
            _menu?.UpdateState(machineState);
    }
}
