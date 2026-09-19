// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Chat;

/// <summary>
/// Raised whenever a chat message is sent, contains the font id, font size, font colour and the sender's EntityUid.
/// </summary>
public sealed class TransformSpeakerFontEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string? FontId;
    public int? FontSize;
    public Color? Color;

    public TransformSpeakerFontEvent(EntityUid sender, string? fontId = null, int? fontSize = null, Color? color = null)
    {
        Sender = sender;
        FontId = fontId;
        FontSize = fontSize;
        Color = color;
    }
}
