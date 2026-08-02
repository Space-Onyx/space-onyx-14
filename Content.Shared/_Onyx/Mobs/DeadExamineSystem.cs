using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Onyx.Mobs;

public sealed partial class DeadExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<MobStateComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.CurrentState != MobState.Dead ||
            HasComp<MindExaminableComponent>(ent))
            return;

        args.PushMarkup($"[color=red]{Loc.GetString("comp-mind-examined-dead", ("ent", ent.Owner))}[/color]");
    }
}
