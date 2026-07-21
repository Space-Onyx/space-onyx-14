using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaStoppedEvent : EntityEventArgs;
