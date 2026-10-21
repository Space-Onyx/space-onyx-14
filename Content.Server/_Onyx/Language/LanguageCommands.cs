using System.Linq;
using Content.Shared._Onyx.Language;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Language;

public sealed class LanguageListCommand : IConsoleCommand
{
    public string Command => "languagelist";
    public string Description => "Lists known languages.";
    public string Help => "languagelist";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } entity ||
            !IoCManager.Resolve<IEntityManager>().TryGetComponent(entity, out LanguageSpeakerComponent? speaker))
            return;

        shell.WriteLine($"Current: {speaker.CurrentLanguage}");
        shell.WriteLine($"Writing: {speaker.WrittenLanguage}");
        foreach (var (language, abilities) in speaker.Languages.OrderBy(entry => entry.Key.Id))
            shell.WriteLine($"{language}: {abilities}");
        shell.WriteLine($"Universal: {speaker.UniversalAbilities}");
    }
}

public sealed class LanguageSelectCommand : IConsoleCommand
{
    public string Command => "languageselect";
    public string Description => "Selects the spoken language.";
    public string Help => "languageselect <language>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } entity || args.Length != 1)
            return;

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        if (!prototypes.HasIndex<LanguagePrototype>(args[0]) ||
            !IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LanguageSystem>().SetLanguage(entity, args[0]))
            shell.WriteError("Unknown or unavailable language.");
    }
}
