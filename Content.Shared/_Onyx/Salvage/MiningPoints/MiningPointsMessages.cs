using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Salvage.MiningPoints;

[Serializable, NetSerializable]
public sealed class ClaimMiningPointsMessage(uint amount) : BoundUserInterfaceMessage
{
    public readonly uint Amount = amount;
}

[ByRefEvent]
public readonly record struct MiningPointsChangedEvent;
