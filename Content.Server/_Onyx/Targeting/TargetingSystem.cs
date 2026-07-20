using Content.Shared._Onyx.Targeting;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Server._Onyx.Targeting;

public sealed partial class TargetingSystem : SharedTargetingSystem
{
    private const int MaxChangesPerSecond = 10;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly Dictionary<NetUserId, (TimeSpan Start, int Count)> _changes = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TargetChangeRequest>(OnTargetChange);
    }

    private void OnTargetChange(TargetChangeRequest message, EntitySessionEventArgs args)
    {
        if (!_configuration.GetCVar(CCVars.TargetingEnabled) ||
            !IsSelectable(message.RequestedPart) || args.SenderSession.AttachedEntity is not { } actor ||
            !TryComp(actor, out TargetingComponent? targeting) ||
            TryComp(actor, out PartStatusComponent? status) && !status.Parts.GetValueOrDefault(message.RequestedPart).Exists ||
            IsRateLimited(args.SenderSession.UserId))
            return;

        targeting.Target = message.RequestedPart;
        Dirty(actor, targeting);
    }

    private bool IsRateLimited(NetUserId user)
    {
        var now = _timing.RealTime;
        if (!_changes.TryGetValue(user, out var state) || now - state.Start >= TimeSpan.FromSeconds(1))
        {
            _changes[user] = (now, 1);
            return false;
        }

        _changes[user] = (state.Start, state.Count + 1);
        return state.Count >= MaxChangesPerSecond;
    }
}
