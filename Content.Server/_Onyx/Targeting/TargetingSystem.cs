using Content.Shared._Onyx.Targeting;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.HealthExaminable;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Targeting;

public sealed partial class TargetingSystem : SharedTargetingSystem
{
    private const int MaxChangesPerSecond = 10;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HealthExaminableSystem _health = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    private readonly Dictionary<NetUserId, (TimeSpan Start, int Count)> _changes = [];
    private readonly Dictionary<NetUserId, TimeSpan> _lastExamine = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TargetChangeRequest>(OnTargetChange);
        SubscribeNetworkEvent<PartStatusExamineRequest>(OnPartStatusExamine);
    }

    private void OnPartStatusExamine(PartStatusExamineRequest message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } actor ||
            !HasComp<TargetingComponent>(actor) ||
            _mobState.IsIncapacitated(actor) ||
            !TryComp(actor, out HealthExaminableComponent? health) ||
            !TryComp(actor, out DamageableComponent? damage) ||
            _lastExamine.TryGetValue(args.SenderSession.UserId, out var last) && _timing.RealTime - last < TimeSpan.FromSeconds(1))
            return;

        _lastExamine[args.SenderSession.UserId] = _timing.RealTime;
        var text = $"{Loc.GetString("part-status-self-examine-title")}\n{_health.CreateMarkup(actor, actor, health, damage).ToMarkup()}";
        _chat.ChatMessageToOne(ChatChannel.Emotes, text, text, EntityUid.Invalid, false,
            args.SenderSession.Channel, recordReplay: false);
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
