using Content.Server.Chat.Systems;
using Content.Shared._Onyx.Chat;

namespace Content.Server._Onyx.Chat;

public sealed partial class EmoteVisibilitySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SendEmoteMessage>(OnSendEmote);
    }

    private void OnSendEmote(SendEmoteMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } source ||
            !Enum.IsDefined(message.Range) ||
            !Enum.IsDefined(message.Perspective))
            return;

        var options = new EmoteVisibilityOptions(
            message.Range,
            Math.Clamp(message.Radius, EmoteVisibilityOptions.MinRadius, EmoteVisibilityOptions.MaxRadius),
            message.Perspective,
            message.ShowToGhosts);
        _chat.SendEmote(source, message.Message, options, player: args.SenderSession);
    }
}
