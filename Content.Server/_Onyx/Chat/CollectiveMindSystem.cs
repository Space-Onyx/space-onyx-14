using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._Onyx.CollectiveMind;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Chat;

public sealed partial class CollectiveMindSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly Dictionary<(EntityUid Entity, string Channel), int> _numbers = new();
    private readonly Dictionary<string, int> _nextNumbers = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _numbers.Clear();
            _nextNumbers.Clear();
        });
    }

    public void Send(EntityUid source, string message, CollectiveMindPrototype? channel)
    {
        if (channel == null || string.IsNullOrWhiteSpace(message) ||
            _mobState.IsDead(source) ||
            !TryComp<CollectiveMindComponent>(source, out var sourceMind) ||
            !sourceMind.Channels.Contains(channel.ID))
            return;

        var number = GetNumber(source, channel.ID);
        var anonymous = new List<ICommonSession>();
        var named = new List<ICommonSession>();
        var admins = _adminManager.ActiveAdmins.ToHashSet();

        var query = EntityQueryEnumerator<CollectiveMindComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var mind, out var actor))
        {
            if (_mobState.IsDead(uid) || admins.Contains(actor.PlayerSession) ||
                (!mind.HearAll && !mind.Channels.Contains(channel.ID)))
                continue;

            (mind.SeeAllNames ? named : anonymous).Add(actor.PlayerSession);
        }

        var escaped = FormattedMessage.EscapeText(message);
        var anonymousWrap = Loc.GetString("collective-mind-chat-wrap-message",
            ("message", escaped),
            ("channel", channel.LocalizedName),
            ("number", number));
        var namedWrap = Loc.GetString("collective-mind-chat-wrap-message-named",
            ("source", Name(source)),
            ("message", escaped),
            ("channel", channel.LocalizedName));
        var adminWrap = Loc.GetString("collective-mind-chat-wrap-message-admin",
            ("source", Name(source)),
            ("message", escaped),
            ("channel", channel.LocalizedName),
            ("number", number));

        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"Collective mind chat from {ToPrettyString(source):Player}: {message}");

        SendTo(anonymous, message, channel.ShowNames ? namedWrap : anonymousWrap, source, channel.Color);
        SendTo(named, message, namedWrap, source, channel.Color);
        SendTo(admins, message, adminWrap, source, channel.Color);
    }

    public bool Grant(EntityUid uid, ProtoId<CollectiveMindPrototype> channel)
    {
        var component = EnsureComp<CollectiveMindComponent>(uid);
        if (!component.Channels.Add(channel))
            return false;

        component.DefaultChannel ??= channel;
        Dirty(uid, component);
        return true;
    }

    public bool Remove(EntityUid uid, ProtoId<CollectiveMindPrototype> channel)
    {
        if (!TryComp<CollectiveMindComponent>(uid, out var component) || !component.Channels.Remove(channel))
            return false;

        if (component.DefaultChannel == channel)
            component.DefaultChannel = component.Channels.Count > 0
                ? component.Channels.First()
                : (ProtoId<CollectiveMindPrototype>?) null;

        if (component.Channels.Count == 0 && !component.HearAll)
            RemCompDeferred<CollectiveMindComponent>(uid);
        else
            Dirty(uid, component);

        _numbers.Remove((uid, channel.Id));
        return true;
    }

    private int GetNumber(EntityUid source, string channel)
    {
        if (_numbers.TryGetValue((source, channel), out var number))
            return number;

        number = _nextNumbers.GetValueOrDefault(channel) + 1;
        _nextNumbers[channel] = number;
        _numbers[(source, channel)] = number;
        return number;
    }

    private void SendTo(
        IEnumerable<ICommonSession> recipients,
        string message,
        string wrapped,
        EntityUid source,
        Color color)
    {
        _chatManager.ChatMessageToMany(
            ChatChannel.CollectiveMind,
            message,
            wrapped,
            source,
            false,
            true,
            recipients.Select(session => session.Channel),
            color);
    }
}
