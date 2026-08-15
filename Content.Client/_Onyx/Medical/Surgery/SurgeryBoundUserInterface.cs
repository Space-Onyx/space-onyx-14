using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Body.Part;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class SurgeryBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SurgerySystem _system;
    private SurgeryWindow? _window;
    private EntityUid? _part;
    private EntProtoId? _surgery;
    private readonly List<EntProtoId> _history = new();

    public SurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = EntMan.System<SurgerySystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SurgeryWindow>();
        _system.OnRefresh += Refresh;
        _window.OnClose += () => _system.OnRefresh -= Refresh;
        _window.PartsButton.OnPressed += _ => ShowParts();
        _window.SurgeriesButton.OnPressed += _ => ShowSurgeries();
        _window.StepsButton.OnPressed += _ => ShowPreviousSurgery();
        SendMessage(new SurgeryStateRequest());

        if (State is SurgeryBuiState state)
            Update(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is SurgeryBuiState surgery)
            Update(surgery);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is SurgeryStepsStateResponse state)
            RefreshSteps(state);
    }

    private void Update(SurgeryBuiState state)
    {
        if (_window == null)
            return;

        if (_part is { } selectedPart && !state.Choices.ContainsKey(EntMan.GetNetEntity(selectedPart)))
        {
            ShowParts();
            UpdateDisabledPanel();
            return;
        }

        if (_part != null && _surgery != null)
            ShowSurgery(_surgery.Value);
        else if (_part != null)
            ShowSurgeries();
        else
            ShowParts();

        UpdateDisabledPanel();
    }

    private void Refresh()
    {
        if (_window == null)
            return;

        var ready = _system.IsReadyForSurgery(Owner);
        if (ready && _part != null && _surgery != null)
            RequestStepsState();

        UpdateDisabledPanel(ready);
    }

    private void ShowParts()
    {
        if (_window == null || State is not SurgeryBuiState state)
            return;

        _part = null;
        _surgery = null;
        _history.Clear();
        _window.Parts.RemoveAllChildren();

        var parts = new List<(EntityUid Entity, BodyPartComponent Part, string Name)>();
        foreach (var netPart in state.Choices.Keys)
        {
            var entity = EntMan.GetEntity(netPart);
            if (EntMan.TryGetComponent(entity, out BodyPartComponent? part))
                parts.Add((entity, part, EntMan.GetComponent<MetaDataComponent>(entity).EntityName));
        }

        foreach (var part in parts.OrderBy(part => PartOrder(part.Part.PartType)).ThenBy(part => part.Name))
        {
            var button = Choice(Capitalize(part.Name));
            button.Button.OnPressed += _ =>
            {
                _part = part.Entity;
                ShowSurgeries();
            };
            _window.Parts.AddChild(button);
        }

        View(ViewType.Parts);
    }

    private void ShowSurgeries()
    {
        if (_window == null || State is not SurgeryBuiState state || _part == null)
            return;

        var netPart = EntMan.GetNetEntity(_part.Value);
        if (!state.Choices.TryGetValue(netPart, out var surgeryIds))
            return;

        _surgery = null;
        _history.Clear();
        _window.Surgeries.RemoveAllChildren();
        var surgeries = new List<(EntProtoId Id, EntityPrototype Proto, SurgeryComponent Component)>();
        foreach (var id in surgeryIds)
        {
            if (_prototypes.TryIndex<EntityPrototype>(id, out var proto) &&
                proto.TryComp(out SurgeryComponent? component, EntMan.ComponentFactory))
                surgeries.Add((id, proto, component));
        }

        foreach (var surgery in surgeries.OrderBy(surgery => surgery.Component.Priority).ThenBy(surgery => surgery.Proto.Name))
        {
            var button = Choice(surgery.Proto.Name);
            button.Button.OnPressed += _ => ShowSurgery(surgery.Id);
            _window.Surgeries.AddChild(button);
        }

        View(ViewType.Surgeries);
    }

    private void ShowSurgery(EntProtoId surgeryId)
    {
        if (_window == null || _part == null ||
            !_prototypes.TryIndex<EntityPrototype>(surgeryId, out var surgeryProto) ||
            !surgeryProto.TryComp(out SurgeryComponent? surgery, EntMan.ComponentFactory))
            return;

        _surgery = surgeryId;
        _window.Steps.RemoveAllChildren();
        var netPart = EntMan.GetNetEntity(_part.Value);

        if (surgery.Requirement is { } requirement && _prototypes.TryIndex<EntityPrototype>(requirement, out var requirementProto))
        {
            var required = Choice(Loc.GetString("surgery-ui-requires", ("surgery", requirementProto.Name)));
            var completed = State is SurgeryBuiState state &&
                            state.Completed.TryGetValue(netPart, out var partCompleted) &&
                            partCompleted.Contains(requirement);
            required.Button.Modulate = Color.FromHex(completed ? "#1F5D37" : "#F2C94C");
            required.Button.Disabled = completed;
            if (!completed)
            {
                required.Button.OnPressed += _ =>
                {
                    _history.Add(surgeryId);
                    ShowSurgery(requirement);
                };
            }
            _window.Steps.AddChild(required);
        }

        foreach (var stepId in surgery.Steps)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(stepId, out var stepProto))
                continue;

            var stepEntity = _system.GetSingleton(stepId);
            var button = StepChoice(stepId, stepProto.Name, stepEntity);
            button.Button.OnPressed += _ => SendPredictedMessage(new SurgeryStepChosenBuiMsg(netPart, surgeryId, stepId));
            _window.Steps.AddChild(button);
        }

        View(ViewType.Steps);
        RequestStepsState();
    }

    private SurgeryChoiceControl Choice(string text, EntityUid? icon = null)
    {
        var control = new SurgeryChoiceControl();
        var texture = icon is { } entity && EntMan.TryGetComponent(entity, out SpriteComponent? sprite)
            ? sprite.Icon?.Default
            : null;
        control.Set(text, texture);
        return control;
    }

    private SurgeryStepButton StepChoice(EntProtoId stepId, string text, EntityUid? icon)
    {
        var control = new SurgeryStepButton { StepId = stepId };
        control.Button.Disabled = true;
        var texture = icon is { } entity && EntMan.TryGetComponent(entity, out SpriteComponent? sprite)
            ? sprite.Icon?.Default
            : null;
        control.Set(text, texture);
        return control;
    }

    private void ShowPreviousSurgery()
    {
        if (_history.Count == 0)
            return;

        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        ShowSurgery(previous);
    }

    private void UpdateDisabledPanel(bool? lyingDown = null)
    {
        if (_window == null)
            return;

        _window.DisabledPanel.Visible = !(lyingDown ?? _system.IsReadyForSurgery(Owner));
        _window.DisabledPanel.MouseFilter = _window.DisabledPanel.Visible ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore;
        if (_window.DisabledPanel.Visible)
        {
            var message = new FormattedMessage();
            message.AddMarkupOrThrow(Loc.GetString("surgery-ui-patient-must-lie"));
            _window.DisabledLabel.SetMessage(message);
        }
    }

    private void View(ViewType view)
    {
        if (_window == null)
            return;

        _window.Parts.Visible = view == ViewType.Parts;
        _window.Surgeries.Visible = view == ViewType.Surgeries;
        _window.Steps.Visible = view == ViewType.Steps;
        _window.PartsButton.Disabled = view == ViewType.Parts;
        _window.SurgeriesButton.Disabled = view != ViewType.Steps;
        _window.StepsButton.Disabled = view != ViewType.Steps || _history.Count == 0;

        _window.SectionTitle.Text = Loc.GetString(view switch
        {
            ViewType.Parts => "surgery-ui-section-parts",
            ViewType.Surgeries => "surgery-ui-section-surgeries",
            _ => "surgery-ui-section-steps",
        });
        var partName = _part is { } part ? Capitalize(EntMan.GetComponent<MetaDataComponent>(part).EntityName) : null;
        var surgeryName = _surgery is { } surgery && _prototypes.TryIndex<EntityPrototype>(surgery, out var proto) ? proto.Name : null;
        _window.ContextLabel.Text = surgeryName != null
            ? Loc.GetString("surgery-ui-context-full", ("part", partName!), ("surgery", surgeryName))
            : partName != null
                ? Loc.GetString("surgery-ui-context-part", ("part", partName))
                : Loc.GetString("surgery-ui-context-none");
    }

    private static int PartOrder(BodyPartType part) => part switch
    {
        BodyPartType.Head => 1,
        BodyPartType.Torso or BodyPartType.Chest => 2,
        BodyPartType.Groin => 3,
        BodyPartType.Arm => 3,
        BodyPartType.Hand => 4,
        BodyPartType.Leg => 5,
        BodyPartType.Foot => 6,
        BodyPartType.Tail => 7,
        _ => 8,
    };

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : OopsConcat(char.ToUpper(text[0]).ToString(), text.Remove(0, 1));

    private static string OopsConcat(string a, string b)
    {
        // Prevent Roslyn from emitting string span code forbidden by the content sandbox.
        return a + b;
    }

    private static string InvalidReason(StepInvalidReason reason) => Loc.GetString(reason switch
    {
        StepInvalidReason.OutOfRange => "surgery-ui-reason-out-of-range",
        StepInvalidReason.NeedsOperatingTable => "surgery-ui-reason-operating-table",
        StepInvalidReason.Clothing => "surgery-ui-reason-clothing",
        StepInvalidReason.MissingTool => "surgery-ui-reason-tool",
        StepInvalidReason.IncompatibleTransplant => "surgery-ui-reason-incompatible-transplant",
        StepInvalidReason.AmputationConsequence => "surgery-ui-reason-amputation-consequence",
        _ => "surgery-ui-reason-unavailable",
    });

    private void RequestStepsState()
    {
        if (_part == null || _surgery == null)
            return;

        SendMessage(new SurgeryStepsStateRequest(EntMan.GetNetEntity(_part.Value), _surgery.Value));
    }

    private void RefreshSteps(SurgeryStepsStateResponse state)
    {
        if (_window == null || _part == null || _surgery == null ||
            state.Part != EntMan.GetNetEntity(_part.Value) || state.Surgery != _surgery)
            return;

        if (state.NextStep < 0 && state.Completed.All(static completed => completed))
        {
            if (_history.Count > 0)
                ShowPreviousSurgery();
            else if (ChangesBodyParts(_surgery.Value))
                ShowParts();
            else
                ShowSurgeries();
            return;
        }

        var index = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton button ||
                !_prototypes.TryIndex<EntityPrototype>(button.StepId, out var stepProto))
                continue;

            if (index >= state.Completed.Count)
                return;

            var complete = state.Completed[index];
            var isNext = state.NextStep == index;
            var wrongTool = state.Reason == StepInvalidReason.MissingTool;

            button.Button.Disabled = !isNext || (!state.Available && !wrongTool);
            button.Button.Modulate = complete ? Color.Green : Color.White;
            button.ToolTip = isNext && !state.Available && !wrongTool ? state.Popup : null;

            var name = stepProto.Name;
            if (isNext && !state.Available && !wrongTool)
                name = $"{stepProto.Name} ({InvalidReason(state.Reason)})";

            button.Set(name, button.Texture.Texture);
            index++;
        }
    }

    private bool ChangesBodyParts(EntProtoId surgeryId)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(surgeryId, out var surgeryProto) ||
            !surgeryProto.TryComp(out SurgeryComponent? surgery, EntMan.ComponentFactory))
            return false;

        foreach (var stepId in surgery.Steps)
        {
            if (_system.GetSingleton(stepId) is { } step &&
                (EntMan.HasComponent<SurgeryDetachPartEffectComponent>(step) ||
                 EntMan.HasComponent<SurgeryAttachPartEffectComponent>(step)))
                return true;
        }

        return false;
    }

    private enum ViewType { Parts, Surgeries, Steps }
}
