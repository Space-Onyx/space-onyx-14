using Content.Shared._Onyx.Bonfire.Components;
using Content.Shared.Placeable;
using Content.Server.Temperature.Systems;

namespace Content.Server._Onyx.Bonfire.Systems;

public sealed partial class BonfireSystem : EntitySystem
{
    [Dependency] private TemperatureSystem _temperature = default!;

    public override void Update(float deltaTime)
    {
        var query = EntityQueryEnumerator<BonfireComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out var bonfire, out var placer))
        {
            var energy = bonfire.HeatPerSecond * deltaTime;
            foreach (var ent in placer.PlacedEntities)
            {
                _temperature.ChangeHeat(ent, energy);
            }
        }
    }
}
