using System.Collections.Generic;
using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.NPC;
using JetBrains.Annotations;
using Content.Client.NPC;
using Content.Shared._Onyx.NPC;

namespace Content.Client._Onyx.NPC;

[UsedImplicitly]
public sealed class NpcFactionEditorEui : BaseEui
{
    private readonly NpcFactionEditorWindow _window;

    public NpcFactionEditorEui()
    {
        _window = new NpcFactionEditorWindow(this);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        _window.SetState((NpcFactionEditorEuiState) state);
    }

    public void ApplyChanges(List<string> factions, List<string> friendlyOverrides, List<string> hostileOverrides)
    {
        SendMessage(new NpcFactionEditorEuiMsg.ApplyChanges(factions, friendlyOverrides, hostileOverrides));
    }
}
