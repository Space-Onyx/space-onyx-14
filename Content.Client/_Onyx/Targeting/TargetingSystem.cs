using Content.Shared._Onyx.Targeting;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Targeting;

public sealed partial class TargetingSystem : SharedTargetingSystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TargetBodyPart? _pending;
    private TimeSpan _pendingUntil;
    private EntityUid? _trackedLocalEntity;
    private bool _hadTargeting;

    public event Action? Updated;
    public TargetBodyPart? Pending => _pending;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TargetingComponent, ComponentStartup>(OnTargetingLifecycle);
        SubscribeLocalEvent<TargetingComponent, ComponentShutdown>(OnTargetingLifecycle);
        SubscribeLocalEvent<TargetingComponent, AfterAutoHandleStateEvent>(OnTargetState);
        SubscribeLocalEvent<PartStatusComponent, AfterAutoHandleStateEvent>(OnStatusState);
        _player.LocalPlayerAttached += OnPlayerChanged;
        _player.LocalPlayerDetached += OnPlayerChanged;
        Bind(ContentKeyFunctions.TargetHead, TargetBodyPart.Head);
        Bind(ContentKeyFunctions.TargetChest, TargetBodyPart.Chest);
        Bind(ContentKeyFunctions.TargetGroin, TargetBodyPart.Groin);
        Bind(ContentKeyFunctions.TargetLeftArm, TargetBodyPart.LeftArm);
        Bind(ContentKeyFunctions.TargetLeftHand, TargetBodyPart.LeftHand);
        Bind(ContentKeyFunctions.TargetRightArm, TargetBodyPart.RightArm);
        Bind(ContentKeyFunctions.TargetRightHand, TargetBodyPart.RightHand);
        Bind(ContentKeyFunctions.TargetLeftLeg, TargetBodyPart.LeftLeg);
        Bind(ContentKeyFunctions.TargetLeftFoot, TargetBodyPart.LeftFoot);
        Bind(ContentKeyFunctions.TargetRightLeg, TargetBodyPart.RightLeg);
        Bind(ContentKeyFunctions.TargetRightFoot, TargetBodyPart.RightFoot);
    }

    public override void Shutdown()
    {
        _player.LocalPlayerAttached -= OnPlayerChanged;
        _player.LocalPlayerDetached -= OnPlayerChanged;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        var local = _player.LocalEntity;
        var hasTargeting = local is { } entity && HasComp<TargetingComponent>(entity);
        if (_trackedLocalEntity != local || _hadTargeting != hasTargeting)
        {
            _trackedLocalEntity = local;
            _hadTargeting = hasTargeting;
            _pending = null;
            Updated?.Invoke();
        }

        if (_pending is not null && _timing.CurTime >= _pendingUntil)
        {
            _pending = null;
            Updated?.Invoke();
        }
    }

    public void Request(TargetBodyPart part)
    {
        if (!IsSelectable(part) || _player.LocalEntity is not { } local || !HasComp<TargetingComponent>(local))
            return;
        _pending = part;
        _pendingUntil = _timing.CurTime + TimeSpan.FromSeconds(2);
        Updated?.Invoke();
        RaiseNetworkEvent(new TargetChangeRequest(part));
    }

    public bool TryGetLocal(out TargetBodyPart selected, out IReadOnlyDictionary<TargetBodyPart, PartStatus> statuses)
    {
        selected = TargetBodyPart.Chest;
        statuses = new Dictionary<TargetBodyPart, PartStatus>();
        if (_player.LocalEntity is not { } local || !TryComp(local, out TargetingComponent? targeting))
            return false;
        selected = targeting.Target;
        if (TryComp(local, out PartStatusComponent? status))
            statuses = status.Parts;
        return true;
    }

    private void Bind(BoundKeyFunction function, TargetBodyPart part) =>
        _input.SetInputCommand(function, InputCmdHandler.FromDelegate(_ => Request(part)));

    private void OnPlayerChanged(EntityUid uid)
    {
        _pending = null;
        Updated?.Invoke();
    }

    private void OnTargetState(Entity<TargetingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;
        _pending = null;
        Updated?.Invoke();
    }

    private void OnTargetingLifecycle(Entity<TargetingComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity == ent.Owner)
            Updated?.Invoke();
    }

    private void OnTargetingLifecycle(Entity<TargetingComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            Updated?.Invoke();
    }

    private void OnStatusState(Entity<PartStatusComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity == ent.Owner)
            Updated?.Invoke();
    }
}
