using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Chat;

[Serializable, NetSerializable]
public sealed class PlayCustomEmoteMessage(
    string message,
    EmoteVisibilityRange range,
    int radius,
    EmotePerspective perspective,
    bool showToGhosts,
    string? soundId) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly EmoteVisibilityRange Range = range;
    public readonly int Radius = radius;
    public readonly EmotePerspective Perspective = perspective;
    public readonly bool ShowToGhosts = showToGhosts;
    public readonly string? SoundId = soundId;
}
