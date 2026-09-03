using Content.Shared._Onyx.Chat;

namespace Content.Client._Onyx.Chat;

public sealed class EmoteVisibilitySystem : EntitySystem
{
    public void SendEmote(string message, EmoteVisibilityOptions options)
    {
        RaiseNetworkEvent(new SendEmoteMessage(message, options.Range, options.Radius, options.Perspective, options.ShowToGhosts));
    }
}
