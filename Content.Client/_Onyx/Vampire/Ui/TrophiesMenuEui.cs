// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Vampire;
using JetBrains.Annotations;

namespace Content.Client._Onyx.Vampire.Ui;

[UsedImplicitly]
public sealed partial class TrophiesMenuEui : BaseEui
{
    private TrophiesMenu _menu;

    public TrophiesMenuEui()
    {
        _menu = new TrophiesMenu();
    }

    public override void Opened()
    {
        _menu.OpenCenteredLeft();
    }

    public override void Closed()
    {
        _menu.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not TrophiesEuiState trophiesState)
            return;

        _menu.Populate(trophiesState);
    }
}
