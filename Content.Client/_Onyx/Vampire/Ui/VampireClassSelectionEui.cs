// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Client.Eui;
using Content.Shared.Vampire;
using JetBrains.Annotations;

namespace Content.Client._Onyx.Vampire.Ui;

[UsedImplicitly]
public sealed partial class VampireClassSelectionEui : BaseEui
{
    private readonly VampireClassSelectionMenu _menu;

    public VampireClassSelectionEui()
    {
        _menu = new VampireClassSelectionMenu();
        _menu.OnClassSelected += className =>
        {
            SendMessage(new VampireClassSelectedMessage(className));
            _menu.Close();
        };
    }

    public override void Opened()
    {
        _menu.OpenCentered();
    }

    public override void Closed()
    {
        _menu.Close();
    }
}
