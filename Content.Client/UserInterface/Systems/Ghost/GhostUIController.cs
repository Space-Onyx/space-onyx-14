using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared._Onyx.Ghost;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed partial class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // <Onyx-Ghost>
    [Dependency] private IGameTiming _timing = default!; // <Onyx-Ghost>

    [UISystemDependency] private readonly GhostSystem? _system = default;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;
        Gui.Update(_system?.AvailableGhostRoleCount, _system?.Player?.CanReturnToBody);
        UpdateReturnToLobbyButton(); // <Onyx-Ghost>
    }

    // <Onyx-Ghost>
    public override void FrameUpdate(FrameEventArgs args)
    {
        if (Gui == null || _system?.IsGhost != true || !Gui.Visible)
            return;
        UpdateReturnToLobbyButton();
    }
    // </Onyx-Ghost>

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.UpdateWarps(msg.Warps);
        window.Populate();
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomFollowedClicked()
    {
        var msg = new WarpToRandomFollowedRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomClicked()
    {
        var msg = new WarpToRandomRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.ReturnToLobbyPressed += ReturnToLobbyPressed; // <Onyx-Ghost>
        Gui.TargetWindow.WarpClicked += OnWarpClicked;
        Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;
        Gui.TargetWindow.OnWarpToRandomFollowedClicked += OnWarpToRandomFollowedClicked;
        Gui.TargetWindow.OnWarpToRandomClicked += OnWarpToRandomClicked;
        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.GhostRolesPressed -= GhostRolesPressed;
        Gui.ReturnToLobbyPressed -= ReturnToLobbyPressed; // <Onyx-Ghost>
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;

        Gui.Hide();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    private void RequestWarps()
    {
        _system?.RequestWarps();
        Gui?.TargetWindow.Populate();
        Gui?.TargetWindow.OpenCentered();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }

    // <Onyx-Ghost>
    private void UpdateReturnToLobbyButton()
    {
        if (Gui == null)
            return;

        if (!_cfg.GetCVar(CCVars.GhostReturnToLobbyEnabled)
            || _system?.Player is not { } player)
        {
            Gui.UpdateReturnToLobbyButton(false, false, Loc.GetString("ghost-return-to-lobby-button-ready"));
            return;
        }

        var canReturn = player.CanReturnToLobby;
        var remaining = GhostReturnToLobbyLogic.GetRemaining(_timing.CurTime, player.ReturnToLobbyAvailableAt);

        var text = Loc.GetString("ghost-return-to-lobby-button-ready");
        if (!canReturn && remaining > TimeSpan.Zero)
        {
            var totalSeconds = (int) System.Math.Ceiling(remaining.TotalSeconds);
            if (totalSeconds < 0)
                totalSeconds = 0;

            var minutes = (totalSeconds / 60).ToString("00");
            var seconds = (totalSeconds % 60).ToString("00");
            text = Loc.GetString("ghost-return-to-lobby-button-timer", ("minutes", minutes), ("seconds", seconds));
        }

        Gui.UpdateReturnToLobbyButton(true, canReturn, text);
    }
    private void ReturnToLobbyPressed()
    {
        _system?.ReturnToLobby();
    }
    // </Onyx-Ghost>
}
