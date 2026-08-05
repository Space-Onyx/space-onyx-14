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
        if (language.Id == "Universal")
        {
            EnsureComp<UniversalLanguageSpeakerComponent>(input);
            EnsureComp<LanguageSpeakerComponent>(input);
            Languages.UpdateLanguages(input);
            return input;
        }

        var knowledge = EnsureComp<LanguageKnowledgeComponent>(input);
        EnsureComp<LanguageSpeakerComponent>(input);

        if (canSpeak)
            knowledge.SpokenLanguages.Add(language);
        if (canUnderstand)
            knowledge.UnderstoodLanguages.Add(language);

        Languages.UpdateLanguages(input);
        return input;
    }

    [CommandImplementation("rm")]
    public EntityUid RemoveLanguage(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<LanguagePrototype> language,
        [CommandArgument] bool removeSpeak = true,
        [CommandArgument] bool removeUnderstand = true)
    {
        if (language.Id == "Universal" && HasComp<UniversalLanguageSpeakerComponent>(input))
            RemComp<UniversalLanguageSpeakerComponent>(input);

        if (TryComp<LanguageKnowledgeComponent>(input, out var knowledge))
        {
            if (removeSpeak)
                knowledge.SpokenLanguages.Remove(language);
            if (removeUnderstand)
                knowledge.UnderstoodLanguages.Remove(language);
        }

        EnsureComp<LanguageSpeakerComponent>(input);
        Languages.UpdateLanguages(input);
        return input;
    }

    [CommandImplementation("lsspoken")]
    public IEnumerable<ProtoId<LanguagePrototype>> ListSpoken([PipedArgument] EntityUid input)
    {
        return TryComp<LanguageSpeakerComponent>(input, out var speaker)
            ? speaker.SpokenLanguages
            : [];
    }

    [CommandImplementation("lsunderstood")]
    public IEnumerable<ProtoId<LanguagePrototype>> ListUnderstood([PipedArgument] EntityUid input)
    {
        return TryComp<LanguageSpeakerComponent>(input, out var speaker)
            ? speaker.UnderstoodLanguages
            : [];
    }
}
