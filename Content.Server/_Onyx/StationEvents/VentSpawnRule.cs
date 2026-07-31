using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
namespace Content.Server._Onyx.StationEvents;

[RegisterComponent]
public sealed partial class VentSpawnRuleComponent : Component;

public sealed partial class VentSpawnRuleSystem : StationEventSystem<VentSpawnRuleComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    private void OnSelectLocation(Entity<VentSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (!TryGetRandomStation(out var station))
        {
            ForceEndSelf(ent, Comp<GameRuleComponent>(args.GameRule));
            return;
        }

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (transform.Anchored && CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
                args.Coordinates.Add(_transform.GetMapCoordinates(transform));
        }

        if (args.Coordinates.Count == 0)
            ForceEndSelf(ent, Comp<GameRuleComponent>(args.GameRule));
    }
}
