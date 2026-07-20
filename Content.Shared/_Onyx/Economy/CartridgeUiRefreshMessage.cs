using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;

[Serializable, NetSerializable]
public sealed class CartridgeUiRefreshMessage : CartridgeMessageEvent
{
    // Пустой класс, просто сигнал для обновления UI
}
