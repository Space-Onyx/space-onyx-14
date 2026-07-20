using Content.Shared._GoobStation.Disease.Components;

namespace Content.Shared._GoobStation.Disease.Chemistry;

public sealed partial class MetabolismImmunityModifierSystem : EntitySystem
{
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImmunityComponent, ImmunityModifierMetabolismComponent>();
        while (query.MoveNext(out var uid, out var immunity, out var modifier))
        {
            if (modifier.ModifierTimer > now)
            {
                immunity.ImmunityGainRate += modifier.GainRateModifier * frameTime;
                immunity.ImmunityStrength += modifier.StrengthModifier * frameTime;
                continue;
            }
            RemCompDeferred<ImmunityModifierMetabolismComponent>(uid);
        }
    }
}
