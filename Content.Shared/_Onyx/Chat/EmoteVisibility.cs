using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Chat;

[Serializable, NetSerializable]
public enum EmoteVisibilityRange : byte
{
    Radius,
    Surrounding,
}

[Serializable, NetSerializable]
public enum EmotePerspective : byte
{
    FirstPerson,
    ThirdPerson,
}

[Serializable, NetSerializable]
public readonly record struct EmoteVisibilityOptions(
    EmoteVisibilityRange Range,
    int Radius,
    EmotePerspective Perspective,
    bool ShowToGhosts)
{
    public const int MinRadius = 2;
    public const int MaxRadius = 15;

    public static readonly EmoteVisibilityOptions Default = new(
        EmoteVisibilityRange.Surrounding,
        MinRadius,
        EmotePerspective.FirstPerson,
        true);
}

[Serializable, NetSerializable]
public sealed class SendEmoteMessage(
    string message,
    EmoteVisibilityRange range,
    int radius,
    EmotePerspective perspective,
    bool showToGhosts) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly EmoteVisibilityRange Range = range;
    public readonly int Radius = radius;
    public readonly EmotePerspective Perspective = perspective;
    public readonly bool ShowToGhosts = showToGhosts;
}
