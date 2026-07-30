using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Destructible;

[UsedImplicitly, DataDefinition]
public sealed partial class SpawnChasmGridBehavior : IThresholdBehavior
{
    [DataField]
    public EntProtoId Prototype = "FloorChasmOpeningSpawner";

    [DataField]
    public int Size = 3;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var coordinates = system.EntityManager.GetComponent<TransformComponent>(owner).Coordinates;
        var offset = (Size - 1) / 2f;
        for (var x = 0; x < Size; x++)
        for (var y = 0; y < Size; y++)
            system.EntityManager.SpawnEntity(Prototype, coordinates.Offset(new(x - offset, y - offset)));
    }
}
