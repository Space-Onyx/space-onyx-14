// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Genetics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Genetics.Ui;

public sealed partial class MindCommunicationUIController : UIController
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private MindCommunicationPanel? _panel;
    private bool _panelDisposed = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MindCommunicationMenuOpenedEvent>(OnMenuReceived);
    }

    private void OnMenuReceived(MindCommunicationMenuOpenedEvent args, EntitySessionEventArgs eventArgs)
    {
        var session = IoCManager.Resolve<IPlayerManager>().LocalSession;
        var userEntity = _entityManager.GetEntity(args.Uid);

        if (session?.AttachedEntity.HasValue == true && session.AttachedEntity.Value == userEntity)
        {
            ShowPanel();
        }
    }

    public void ShowPanel()
    {
        if (_panel is null)
        {
            _panel = _uiManager.CreateWindow<MindCommunicationPanel>();
            _panel.OnClose += OnMenuClosed;
            _panel.OpenCentered();
        }
        else
        {
            _panel.OpenCentered();
        }

        Timer.Spawn(30000, () =>
        {
            if (_panel != null && !_panelDisposed)
                _panel.Close();
        });
    }

    private void OnMenuClosed()
    {
        _panelDisposed = true;
        _panel = null;
    }
}
