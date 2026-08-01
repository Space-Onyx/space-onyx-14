using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
// <Onyx-AgentIDNanoChat>
using Content.Shared._DV.NanoChat;
// </Onyx-AgentIDNanoChat>
using Content.Shared.StatusIcon;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.UI;

/// <summary>
/// Initializes a <see cref="AgentIDCardWindow"/> and updates it when new server messages are received.
/// </summary>
public sealed class AgentIDCardBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AgentIDCardWindow? _window;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out AgentIDCardComponent? agent))
            return;

        _window = this.CreateWindow<AgentIDCardWindow>();

        _window.OnNameChanged += OnNameChanged;
        _window.OnJobChanged += OnJobChanged;
        _window.OnJobIconChanged += OnJobIconChanged;
        // <Onyx-AgentIDNanoChat>
        _window.OnNumberChanged += OnNumberChanged;
        // </Onyx-AgentIDNanoChat>

        ProtoId<JobIconPrototype> currentIcon = default;
        if (EntMan.TryGetComponent<IdCardComponent>(Owner, out var card))
            currentIcon = card.JobIcon;

        _window.SetAllowedIcons(agent.IconGroups, currentIcon);
        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_window == null)
            return;

        if (!EntMan.TryGetComponent<IdCardComponent>(Owner, out var card))
            return;

        // <Onyx-AgentIDNanoChat-edited>
        EntMan.TryGetComponent<NanoChatCardComponent>(Owner, out var nanoChat);
        _window.Update(card, nanoChat?.Number);
        // </Onyx-AgentIDNanoChat-edited>
    }

    private void OnNameChanged(string newName)
    {
        SendPredictedMessage(new AgentIDCardNameChangedMessage(newName));
    }

    // <Onyx-AgentIDNanoChat>
    private void OnNumberChanged(uint newNumber)
    {
        SendPredictedMessage(new AgentIDCardNumberChangedMessage(newNumber));
    }
    // </Onyx-AgentIDNanoChat>

    private void OnJobChanged(string newJob)
    {
        SendPredictedMessage(new AgentIDCardJobChangedMessage(newJob));
    }

    private void OnJobIconChanged(ProtoId<JobIconPrototype> newJobIconId)
    {
        SendPredictedMessage(new AgentIDCardJobIconChangedMessage(newJobIconId));
    }
}
