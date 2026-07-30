using Content.Server._Onyx.StationEvents.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server._Onyx.StationEvents.Events;

public sealed partial class RandomMultipleSpawnRule : StationEventSystem<RandomMultipleSpawnRuleComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    protected override void Started(EntityUid uid, RandomMultipleSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var amount = _random.Next(component.MinAmount, component.MaxAmount + 1);
        for (var i = 0; i < amount; i++)
        {
            if (!TryFindRandomTile(out _, out _, out _, out var coordinates))
                continue;

            Sawmill.Info($"Spawning {component.Prototype} at {coordinates}");
            Spawn(component.Prototype, coordinates);
        }
    }
}
