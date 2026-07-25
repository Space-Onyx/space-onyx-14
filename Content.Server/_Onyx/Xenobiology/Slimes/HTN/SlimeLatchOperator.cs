using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.DoAfter;

namespace Content.Server._Onyx.Xenobiology.Slimes.HTN;

public sealed partial class SlimeLatchOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private SharedDoAfterSystem _doAfter = default!;
    private SlimeLatchSystem _latch = default!;

    [DataField(required: true)]
    public string LatchKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
        _latch = sysManager.GetEntitySystem<SlimeLatchSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<XenobioSlimeComponent>(owner, out var slime) ||
            !blackboard.TryGetValue<EntityUid>(LatchKey, out var target, _entManager))
            return HTNOperatorStatus.Failed;

        if (_latch.IsLatched((owner, slime), target))
            return HTNOperatorStatus.Finished;

        if (slime.LastLatchDoAfterId is { } id)
        {
            var status = _doAfter.GetStatus(id);
            if (status == DoAfterStatus.Running)
                return HTNOperatorStatus.Continuing;

            slime.LastLatchDoAfterId = null;
            return status == DoAfterStatus.Finished && slime.LastLatchSucceeded
                ? HTNOperatorStatus.Finished
                : HTNOperatorStatus.Failed;
        }

        return _latch.TryStartLatch((owner, slime), target)
            ? HTNOperatorStatus.Continuing
            : HTNOperatorStatus.Failed;
    }
}
