using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> BarksEnabled =
        CVarDef.Create("barks.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SpeechBubbleRevealEnabled =
        CVarDef.Create("barks.speech_bubble_reveal_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMaxSpeechDuration =
        CVarDef.Create("barks.max_speech_duration", 4f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksShoutSpeedMultiplier =
        CVarDef.Create("barks.shout_speed_multiplier", 0.7f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksCommaPause =
        CVarDef.Create("barks.comma_pause", 0.15f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksSentencePause =
        CVarDef.Create("barks.sentence_pause", 0.25f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksEllipsisPause =
        CVarDef.Create("barks.ellipsis_pause", 0.55f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMaxPitch =
        CVarDef.Create("barks.max_pitch", 1.5f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMinPitch =
        CVarDef.Create("barks.min_pitch", 0.6f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMinDelay =
        CVarDef.Create("barks.min_delay", 0.1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMaxDelay =
        CVarDef.Create("barks.max_delay", 0.6f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ReplaceTTSWithBarks =
        CVarDef.Create("barks.replace_tts", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksVolume =
        CVarDef.Create("barks.volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksRadioVolume =
        CVarDef.Create("barks.radio_volume", 0.25f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
