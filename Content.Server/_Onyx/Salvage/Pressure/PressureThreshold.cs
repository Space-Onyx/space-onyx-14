using Content.Server.Atmos.EntitySystems;
using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Shared._Onyx.Salvage.Pressure;
using Content.Shared.EntityConditions;

namespace Content.Server._Onyx.Salvage.Pressure;

public sealed partial class PressureThresholdSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, EntityConditionEvent<PressureThreshold>>(OnCondition);
    }
    private void OnCondition(Entity<TransformComponent> ent, ref EntityConditionEvent<PressureThreshold> args)
    {
        var onLavaland = args.Condition.WorksOnLavaland &&
            ent.Comp.MapUid is { } map &&
            HasComp<LavalandMapComponent>(map);
        var pressure = _atmos.GetTileMixture((ent.Owner, ent.Comp))?.Pressure ?? 0f;
        args.Result = onLavaland || (pressure >= args.Condition.Min && pressure <= args.Condition.Max);
    }
}
