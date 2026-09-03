using Content.Shared._Onyx.Chat;
using Content.Shared.Chat;
using Content.Shared.Tag;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> EmoteVisibilityGhostTag = "EmoteVisibilityGhost";
    private static readonly ProtoId<TagPrototype> EmoteVisibilityBypassTag = "EmoteVisibilityBypass";

    public void SendEmote(
        EntityUid source,
        string message,
        EmoteVisibilityOptions options,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        string? nameOverride = null,
        bool hideLog = false,
        bool ignoreActionBlocker = false)
    {
        TrySendInGameICMessage(
            source,
            message,
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog,
            shell,
            player,
            nameOverride,
            ignoreActionBlocker: ignoreActionBlocker,
            emoteVisibility: options);
    }
}
