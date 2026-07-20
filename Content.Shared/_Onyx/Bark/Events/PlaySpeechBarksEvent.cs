using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.SpeechBarks;

[Serializable, NetSerializable]
public sealed class PlaySpeechBarksEvent : EntityEventArgs
{
    public NetEntity? Source;
    public NetEntity? Emitter;
    public string? Message;
    public SoundSpecifier SoundSpecifier;
    public float Pitch;
    public float LowVar;
    public float HighVar;
    public bool IsWhisper;
    public bool IsRadio;
    public float VolumeScale;

    public PlaySpeechBarksEvent(
        NetEntity source,
        string? message,
        SoundSpecifier soundSpecifier,
        float pitch,
        float lowVar,
        float highVar,
        bool isWhisper,
        bool isRadio = false,
        NetEntity? emitter = null,
        float volumeScale = 1f)
    {
        Source = source;
        Emitter = emitter;
        Message = message;
        SoundSpecifier = soundSpecifier;
        Pitch = pitch;
        LowVar = lowVar;
        HighVar = highVar;
        IsWhisper = isWhisper;
        IsRadio = isRadio;
        VolumeScale = volumeScale;
    }
}
