using Content.Server.Construction;
using Content.Shared.Atmos;
using Content.Shared._Onyx.Construction;

namespace Content.Server._Onyx.Construction;

public sealed partial class ChangeConstructionNodeOnIgniteSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangeConstructionNodeOnIgniteComponent, IgnitedEvent>(OnIgnited);
    }

    private void OnIgnited(Entity<ChangeConstructionNodeOnIgniteComponent> ent, ref IgnitedEvent args)
    {
        _construction.ChangeNode(ent, null, ent.Comp.TargetNode);
    }
}
