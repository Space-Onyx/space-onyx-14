// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Actions;

namespace Content.Shared.Shaders;

public sealed partial class SharedNaturalNightVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NaturalNightVisionComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<NaturalNightVisionComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Action = _action.AddAction(ent, ent.Comp.ActionProto);
        Dirty(ent.Owner, ent.Comp);
    }
}
