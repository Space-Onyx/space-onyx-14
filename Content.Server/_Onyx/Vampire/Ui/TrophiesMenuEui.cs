// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.EUI;
using Content.Shared.Vampire;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server.Vampire;

[UsedImplicitly]
public sealed partial class TrophiesMenuEui : BaseEui
{
    private readonly TrophiesEuiState _state;

    public TrophiesMenuEui(TrophiesEuiState state)
    {
        _state = state;
    }

    public override EuiStateBase GetNewState() => _state;
}
