using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> SpeechSoundsEnabled =
        CVarDef.Create("audio.speech_sounds_enabled", false, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpeechBubbleRevealMinSpeed =
        CVarDef.Create("speech.speech_bubble_reveal_min_speed", 5f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpeechBubbleRevealMaxSpeed =
        CVarDef.Create("speech.speech_bubble_reveal_max_speed", 40f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SpeechBubbleRevealEnabled =
        CVarDef.Create("speech.speech_bubble_reveal_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpeechBubbleCommaPause =
        CVarDef.Create("speech.speech_bubble_comma_pause", 0.15f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpeechBubbleSentencePause =
        CVarDef.Create("speech.speech_bubble_sentence_pause", 0.25f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpeechBubbleEllipsisPause =
        CVarDef.Create("speech.speech_bubble_ellipsis_pause", 0.55f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
