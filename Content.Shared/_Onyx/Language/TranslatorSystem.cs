using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Language;

public sealed partial class TranslatorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandheldTranslatorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<HandheldTranslatorComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.ShowInfoOnExamine)
            return;

        var understood = ent.Comp.Languages.Where(entry => (entry.Value & LanguageAbility.Understand) != 0).Select(entry => GetLanguageName(entry.Key));
        var spoken = ent.Comp.Languages.Where(entry => (entry.Value & LanguageAbility.Speak) != 0).Select(entry => GetLanguageName(entry.Key));
        var required = ent.Comp.Requires.Select(GetLanguageName);

        args.PushMarkup(Loc.GetString("translator-examined-langs-understood", ("languages", string.Join(", ", understood))));
        args.PushMarkup(Loc.GetString("translator-examined-langs-spoken", ("languages", string.Join(", ", spoken))));
        args.PushMarkup(Loc.GetString(ent.Comp.TranslateWriting
            ? "translator-examined-writing-enabled"
            : "translator-examined-writing-disabled"));

        if (ent.Comp.Requires.Count > 0)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.RequiresAll
                    ? "translator-examined-requires-all"
                    : "translator-examined-requires-any",
                ("languages", string.Join(", ", required))));
        }

        args.PushMarkup(Loc.GetString(ent.Comp.Enabled
            ? "translator-examined-enabled"
            : "translator-examined-disabled"));
    }

    public void UpdateAppearance(Entity<HandheldTranslatorComponent> ent)
    {
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, ent.Comp.Enabled);
    }

    private string GetLanguageName(ProtoId<LanguagePrototype> language)
    {
        return Loc.GetString($"language-{language}-name");
    }
}
