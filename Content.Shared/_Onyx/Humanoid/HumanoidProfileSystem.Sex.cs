using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem
{
    public bool SetSex(Entity<HumanoidProfileComponent> entity, Sex sex, bool updateGender)
    {
        if (!ProtoMan.TryIndex(entity.Comp.Species, out SpeciesPrototype? species) ||
            !species.Sexes.Contains(sex))
        {
            return false;
        }

        var oldVoice = entity.Comp.Voice;
        entity.Comp.Sex = sex;

        if ((int) sex < species.DefaultSoundsBySex.Length)
            entity.Comp.Voice = species.DefaultSoundsBySex[(int) sex];

        if (updateGender)
        {
            entity.Comp.Gender = sex switch
            {
                Sex.Male => Gender.Male,
                Sex.Female => Gender.Female,
                _ => Gender.Neuter,
            };

            if (TryComp<GrammarComponent>(entity, out var grammar))
                _grammar.SetGender((entity.Owner, grammar), entity.Comp.Gender);
        }

        Dirty(entity);

        var voiceChanged = new VoiceChangedEvent(oldVoice, entity.Comp.Voice);
        RaiseLocalEvent(entity.Owner, ref voiceChanged);
        return true;
    }
}
