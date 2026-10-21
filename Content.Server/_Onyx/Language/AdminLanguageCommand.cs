using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Onyx.Language;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;

namespace Content.Server._Onyx.Language;

[ToolshedCommand(Name = "language"), AdminCommand(AdminFlags.Admin)]
public sealed class AdminLanguageCommand : ToolshedCommand
{
    private LanguageSystem? _languages;
    private LanguageSystem Languages => _languages ??= GetSys<LanguageSystem>();

    [CommandImplementation("add")]
    public EntityUid AddLanguage(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<LanguagePrototype> language,
        [CommandArgument] bool canSpeak = true,
        [CommandArgument] bool canUnderstand = true)
    {
        var abilities = TryComp<LanguageKnowledgeComponent>(input, out var knowledge)
            ? knowledge.Languages.GetValueOrDefault(language)
            : LanguageAbility.None;
        if (canSpeak)
            abilities |= LanguageAbility.Speak | LanguageAbility.Write;
        if (canUnderstand)
            abilities |= LanguageAbility.Understand | LanguageAbility.Read;
        Languages.SetKnowledge(input, language, abilities);
        return input;
    }

    [CommandImplementation("rm")]
    public EntityUid RemoveLanguage(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<LanguagePrototype> language,
        [CommandArgument] bool removeSpeak = true,
        [CommandArgument] bool removeUnderstand = true)
    {
        if (TryComp<LanguageKnowledgeComponent>(input, out var knowledge))
        {
            var abilities = knowledge.Languages.GetValueOrDefault(language);
            if (removeSpeak)
                abilities &= ~(LanguageAbility.Speak | LanguageAbility.Write);
            if (removeUnderstand)
                abilities &= ~(LanguageAbility.Understand | LanguageAbility.Read);
            Languages.SetKnowledge(input, language, abilities);
        }
        return input;
    }

    [CommandImplementation("set")]
    public EntityUid SetAbilities(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<LanguagePrototype> language,
        [CommandArgument] LanguageAbility abilities)
    {
        Languages.SetKnowledge(input, language, abilities);
        return input;
    }

    [CommandImplementation("lsspoken")]
    public IEnumerable<ProtoId<LanguagePrototype>> ListSpoken([PipedArgument] EntityUid input)
    {
        return TryComp<LanguageSpeakerComponent>(input, out var speaker)
            ? speaker.Languages.Where(entry => (entry.Value & LanguageAbility.Speak) != 0).Select(entry => entry.Key)
            : [];
    }

    [CommandImplementation("lsunderstood")]
    public IEnumerable<ProtoId<LanguagePrototype>> ListUnderstood([PipedArgument] EntityUid input)
    {
        return TryComp<LanguageSpeakerComponent>(input, out var speaker)
            ? speaker.Languages.Where(entry => (entry.Value & LanguageAbility.Understand) != 0).Select(entry => entry.Key)
            : [];
    }
}
