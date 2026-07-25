using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Onyx.Mobs.Growth;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._Onyx.Xenobiology.Slimes.HTN;

public sealed partial class PickSlimeLatchTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private HungerSystem _hunger = default!;
    private SlimeLatchSystem _latch = default!;
    private MobStateSystem _mobState = default!;
    private NpcFactionSystem _factions = default!;
    private PathfindingSystem _pathfinding = default!;

    [DataField(required: true)]
    public string RangeKey = string.Empty;

    [DataField(required: true)]
    public string TargetKey = string.Empty;

    [DataField(required: true)]
    public string LatchKey = string.Empty;

    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _hunger = sysManager.GetEntitySystem<HungerSystem>();
        _latch = sysManager.GetEntitySystem<SlimeLatchSystem>();
        _mobState = sysManager.GetEntitySystem<MobStateSystem>();
        _factions = sysManager.GetEntitySystem<NpcFactionSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entManager) ||
            !_entManager.TryGetComponent<XenobioSlimeComponent>(owner, out var slime) ||
            !_entManager.TryGetComponent<MobGrowthComponent>(owner, out var growth) ||
            slime.LatchedTarget != null ||
            !_entManager.TryGetComponent<HungerComponent>(owner, out var hunger))
            return (false, null);

        var baby = growth.CurrentStage == growth.InitialStage;
        var threshold = _hunger.GetHungerThreshold(hunger);
        if (baby && threshold > HungerThreshold.Peckish)
            return (false, null);

        foreach (var target in _factions.GetNearbyHostiles(owner, range))
        {
            if (_mobState.IsDead(target) ||
                !_latch.CanLatch((owner, slime), target) ||
                target == slime.Tamer && (baby || threshold > HungerThreshold.Peckish))
                continue;

            var path = await _pathfinding.GetPath(owner,
                target,
                1f,
                cancelToken,
                flags: _pathfinding.GetFlags(blackboard));
            if (path.Result != PathResult.Path || !_entManager.TryGetComponent<TransformComponent>(target, out var transform))
                continue;

            return (true, new Dictionary<string, object>
            {
                { TargetKey, transform.Coordinates },
                { LatchKey, target },
                { PathfindKey, path },
            });
        }

        return (false, null);
    }
}
