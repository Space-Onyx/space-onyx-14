using Content.Server.Chat.Systems;
using Content.Shared._Onyx.CollectiveMind;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Chat.Commands;

[AnyCommand]
internal sealed class CollectiveMindSayCommand : IConsoleCommand
{
    public string Command => "cmsay";
    public string Description => Loc.GetString("cmd-cmsay-desc");
    public string Help => Loc.GetString("cmd-cmsay-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not ICommonSession { Status: SessionStatus.InGame, AttachedEntity: { } entity })
        {
            shell.WriteError(Loc.GetString("cmd-cmsay-error-no-entity"));
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!entityManager.TryGetComponent<CollectiveMindComponent>(entity, out var mind) || mind.Channels.Count == 0)
        {
            shell.WriteError(Loc.GetString("cmd-cmsay-error-no-mind"));
            return;
        }

        var mobState = entityManager.System<MobStateSystem>();
        if (mobState.IsDead(entity) || (!mind.CanUseInCrit && mobState.IsCritical(entity)))
        {
            shell.WriteError(Loc.GetString("cmd-cmsay-error-incapacitated"));
            return;
        }

        var message = string.Join(" ", args).Trim();
        if (message.Length == 0)
            return;

        entityManager.System<ChatSystem>().TrySendInGameICMessage(
            entity,
            message,
            InGameICChatType.CollectiveMind,
            ChatTransmitRange.Normal);
    }
}
