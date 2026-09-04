// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.EUI;
using Content.Shared.Vampire;
using Content.Shared.Eui;

namespace Content.Server.Vampire;

public sealed partial class VampireClassSelectionEui : BaseEui
{
    private readonly EntityUid _vampire;
    private readonly VampireSystem _vampireSystem;

    public VampireClassSelectionEui(EntityUid vampire, VampireSystem vampireSystem)
    {
        _vampire = vampire;
        _vampireSystem = vampireSystem;
    }

    public override EuiStateBase GetNewState() => new VampireClassSelectionState();

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is VampireClassSelectedMessage selected)
        {
            _vampireSystem.OnClassSelected(_vampire, selected.SelectedClass);
            Close();
        }
    }
}
