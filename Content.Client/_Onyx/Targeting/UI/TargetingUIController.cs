using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Onyx.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Onyx.Targeting.UI;

public sealed class TargetingUIController : UIController, IOnSystemChanged<TargetingSystem>, IOnStateEntered<GameplayState>
{
    [UISystemDependency] private readonly TargetingSystem _system = default!;
    [UISystemDependency] private readonly SpriteSystem _sprites = default!;

    private TargetingControl? Control => UIManager.GetActiveUIWidgetOrNull<TargetingControl>();
    private PartStatusControl? StatusControl => UIManager.GetActiveUIWidgetOrNull<AlertsUI>()?.PartStatus;

    public override void Initialize()
    {
        base.Initialize();
        var gameplay = UIManager.GetUIController<GameplayStateLoadController>();
        gameplay.OnScreenLoad += OnScreenLoad;
        gameplay.OnScreenUnload += OnScreenUnload;
    }

    public void OnSystemLoaded(TargetingSystem system)
    {
        system.Updated += Refresh;
        AttachControl();
    }

    public void OnSystemUnloaded(TargetingSystem system)
    {
        system.Updated -= Refresh;
        DetachControl();
    }

    public void OnStateEntered(GameplayState state) => AttachControl();

    private void OnScreenLoad() => AttachControl();

    private void OnScreenUnload() => DetachControl();

    private void AttachControl()
    {
        if (Control is { } control)
        {
            control.PartRequested -= OnPartRequested;
            control.PartRequested += OnPartRequested;
        }

        if (StatusControl is { } status)
        {
            status.ExamineRequested -= OnExamineRequested;
            status.ExamineRequested += OnExamineRequested;
        }

        Refresh();
    }

    private void DetachControl()
    {
        if (Control is { } control)
            control.PartRequested -= OnPartRequested;
        if (StatusControl is { } status)
            status.ExamineRequested -= OnExamineRequested;
    }

    private void OnPartRequested(TargetBodyPart part) => _system.Request(part);
    private void OnExamineRequested() => _system.RequestSelfExamine();

    private void Refresh()
    {
        var visible = _system.TryGetLocal(out var selected, out var statuses);
        Control?.Refresh(visible, selected, _system.Pending, statuses);
        StatusControl?.Refresh(visible, statuses, _sprites);
    }
}
