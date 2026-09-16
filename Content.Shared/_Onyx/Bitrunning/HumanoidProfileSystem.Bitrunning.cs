using Content.Shared.Corvax.TTS;
using Content.Shared._Onyx.SpeechBarks;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem
{
    public void ApplyBitrunningProfileFields(EntityUid target, EntityUid source)
    {
        if (!TryComp<HumanoidProfileComponent>(target, out var targetProfile)
            || !TryComp<HumanoidProfileComponent>(source, out var sourceProfile))
            return;

        targetProfile.Height = sourceProfile.Height;
        targetProfile.Width = sourceProfile.Width;
        targetProfile.Gender = sourceProfile.Gender;
        targetProfile.Sex = sourceProfile.Sex;
        targetProfile.Age = sourceProfile.Age;
        targetProfile.Species = sourceProfile.Species;
        targetProfile.TTSVoice = sourceProfile.TTSVoice;

        var oldVoice = targetProfile.Voice;
        targetProfile.Voice = sourceProfile.Voice;
        Dirty(target, targetProfile);

        var voiceChanged = new VoiceChangedEvent(oldVoice, sourceProfile.Voice);
        RaiseLocalEvent(target, ref voiceChanged);

        if (TryComp<GrammarComponent>(target, out var grammar))
            _grammar.SetGender((target, grammar), sourceProfile.Gender);

        if (TryComp<TTSComponent>(target, out var targetTts))
        {
            targetTts.VoicePrototypeId = sourceProfile.TTSVoice;
            Dirty(target, targetTts);
        }

        if (TryComp<SpeechBarksComponent>(target, out var targetBarks)
            && TryComp<SpeechBarksComponent>(source, out var sourceBarks))
        {
            targetBarks.Data = sourceBarks.Data.Copy();
            Dirty(target, targetBarks);
        }
    }

}
