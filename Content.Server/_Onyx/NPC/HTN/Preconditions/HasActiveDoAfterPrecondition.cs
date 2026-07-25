using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.DoAfter;

namespace Content.Server._Onyx.NPC.HTN.Preconditions;

public sealed partial class HasActiveDoAfterPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entityManager = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entityManager.HasComponent<ActiveDoAfterComponent>(owner) ^ Invert;
    }
}
