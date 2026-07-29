using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Shared._Onyx.CollectiveMind; // <Onyx-OSayOptions>
using Content.Shared._Onyx.Language; // <Onyx-OSayOptions>
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Prototypes; // <Onyx-OSayOptions>

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class OSay : LocalizedCommands
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypes = default!; // <Onyx-OSayOptions>

    public override string Command => "osay";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.Components<MetaDataComponent>(args[0]),
                Loc.GetString("osay-command-arg-uid")); // <Onyx-OSayOptions-edited>
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions( Enum.GetNames(typeof(InGameICChatType)),
                Loc.GetString("osay-command-arg-type"));
        }

        if (!TryGetSource(args[0], out var source) || !Enum.TryParse<InGameICChatType>(args[1], true, out var chatType))
            return CompletionResult.Empty;

        // <Onyx-OSayOptions>
        if (chatType == InGameICChatType.CollectiveMind)
        {
            if (args.Length == 3)
                return CompletionResult.FromHintOptions(GetCollectiveMinds(source), Loc.GetString("osay-command-arg-collective-mind"));

            if (args.Length == 4)
                return CompletionResult.FromHintOptions(GetLanguages(source), Loc.GetString("osay-command-arg-language"));

            return CompletionResult.FromHint(Loc.GetString("osay-command-arg-message"));
        }

        if (args.Length == 3)
            return CompletionResult.FromHintOptions(GetLanguages(source), Loc.GetString("osay-command-arg-language-optional"));

        return CompletionResult.FromHint(Loc.GetString("osay-command-arg-message"));
        // </Onyx-OSayOptions>
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine(Loc.GetString("osay-command-error-args"));
            return;
        }

        if (!Enum.TryParse<InGameICChatType>(args[1], true, out var chatType))
        {
            shell.WriteLine(Loc.GetString("osay-command-error-type", ("arg", args[1])));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var sourceNet) || !_entityManager.TryGetEntity(sourceNet, out var source) || !_entityManager.EntityExists(source))
        {
            shell.WriteLine(Loc.GetString("osay-command-error-euid", ("arg", args[0])));
            return;
        }

        // <Onyx-OSayOptions>
        var messageIndex = 2;
        ProtoId<LanguagePrototype>? language = null;
        if (chatType == InGameICChatType.CollectiveMind)
        {
            if (args.Length < 4 || !_entityManager.TryGetComponent<CollectiveMindComponent>(source, out var mind) ||
                !_prototypes.TryIndex<CollectiveMindPrototype>(args[2], out var collectiveMind) ||
                !mind.Channels.Contains(collectiveMind.ID))
            {
                shell.WriteLine(Loc.GetString("osay-command-error-collective-mind", ("arg", args.ElementAtOrDefault(2) ?? string.Empty)));
                return;
            }

            messageIndex = 3;
            if (args.Length > 4 && TryGetLanguage(source.Value, args[3], out language))
                messageIndex = 4;
        }
        else if (TryGetLanguage(source.Value, args[2], out language))
        {
            messageIndex = 3;
        }

        var message = string.Join(" ", args.Skip(messageIndex)).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        if (chatType == InGameICChatType.CollectiveMind)
            message = $"+{_prototypes.Index<CollectiveMindPrototype>(args[2]).KeyCode} {message}";
        // </Onyx-OSayOptions>

        _entityManager.System<ChatSystem>().TrySendInGameICMessage(source.Value, message, chatType, false, languageOverride: language); // <Onyx-OSayOptions-edited>
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{(shell.Player != null ? shell.Player.Name : "An administrator")} forced {_entityManager.ToPrettyString(source.Value)} to {args[1]}{(language is null ? "" : $" in {language}")}: {message}"); // <Onyx-OSayOptions-edited>
    }

    // <Onyx-OSayOptions>
    private bool TryGetSource(string value, out EntityUid source)
    {
        source = default;
        return NetEntity.TryParse(value, out var netEntity) &&
               _entityManager.TryGetEntity(netEntity, out var uid) &&
               uid is { } entity &&
               _entityManager.EntityExists(source = entity);
    }

    private IEnumerable<string> GetCollectiveMinds(EntityUid source)
    {
        return _entityManager.TryGetComponent<CollectiveMindComponent>(source, out var component)
            ? component.Channels.Select(channel => channel.Id).Order()
            : [];
    }

    private IEnumerable<string> GetLanguages(EntityUid source)
    {
        return _entityManager.TryGetComponent<LanguageSpeakerComponent>(source, out var component)
            ? component.SpokenLanguages.Select(language => language.Id).Order()
            : [];
    }

    private bool TryGetLanguage(EntityUid source, string value, out ProtoId<LanguagePrototype>? language)
    {
        language = null;
        if (!_entityManager.TryGetComponent<LanguageSpeakerComponent>(source, out var speaker) ||
            !_prototypes.HasIndex<LanguagePrototype>(value) ||
            !speaker.SpokenLanguages.Contains(value))
            return false;

        language = value;
        return true;
    }
    // </Onyx-OSayOptions>
}
