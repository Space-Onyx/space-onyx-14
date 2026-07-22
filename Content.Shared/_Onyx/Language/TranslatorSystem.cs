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

        var understood = ent.Comp.UnderstoodLanguages.Select(GetLanguageName);
        var spoken = ent.Comp.SpokenLanguages.Select(GetLanguageName);
        var required = ent.Comp.RequiredLanguages.Select(GetLanguageName);

        args.PushMarkup(Loc.GetString("translator-examined-langs-understood", ("languages", string.Join(", ", understood))));
        args.PushMarkup(Loc.GetString("translator-examined-langs-spoken", ("languages", string.Join(", ", spoken))));

        if (ent.Comp.RequiredLanguages.Count > 0)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.RequiresAllLanguages
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
