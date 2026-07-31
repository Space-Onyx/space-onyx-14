using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared._Onyx.Mech;

[RegisterComponent]
public sealed partial class MechPowerBlindnessComponent : Component;

public sealed partial class MechPowerBlindnessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechPowerBlindnessComponent, CanSeeAttemptEvent>(OnCanSee);
    }

    private void OnCanSee(Entity<MechPowerBlindnessComponent> ent, ref CanSeeAttemptEvent args)
    {
        args.Cancel();
    }
}
