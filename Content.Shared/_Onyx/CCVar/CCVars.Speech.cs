using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> SpeechSoundsEnabled =
        CVarDef.Create("audio.speech_sounds_enabled", false, CVar.SERVER | CVar.ARCHIVE);
}
